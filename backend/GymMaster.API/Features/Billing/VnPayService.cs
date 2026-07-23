using System.Globalization;
using System.Security.Claims;
using GymMaster.API.Data;
using GymMaster.API.Entities;
using GymMaster.API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using GymMaster.API.Common;
using GymMaster.API.Features.Dashboard;
using GymMaster.API.Infrastructure;

namespace GymMaster.API.Features.Billing;
public sealed class VnPayService : IVnPayService
{
    private readonly GymMasterDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly VnPayOptions _options;

    public VnPayService(
        GymMasterDbContext dbContext,
        IAuditService auditService,
        IOptions<VnPayOptions> options)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _options = options.Value;
    }

    public async Task<ServiceResult<CreateVnPayPaymentResponse>> CreatePaymentUrlAsync(
        CreateVnPayPaymentRequest request,
        string ipAddress,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actorId = GetActorId(principal);
        if (actorId is null)
        {
            return Fail<CreateVnPayPaymentResponse>("FORBIDDEN", "Khong xac dinh duoc nguoi thao tac.", StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(_options.TmnCode) || string.IsNullOrWhiteSpace(_options.HashSecret))
        {
            return Fail<CreateVnPayPaymentResponse>("VNPAY_NOT_CONFIGURED", "Chua cau hinh VNPay (TmnCode/HashSecret).", StatusCodes.Status500InternalServerError);
        }

        var membership = await _dbContext.Memberships
            .Include(item => item.Package)
            .FirstOrDefaultAsync(item => item.Id == request.MembershipId, cancellationToken);

        if (membership is null)
        {
            return Fail<CreateVnPayPaymentResponse>("NOT_FOUND", "Khong tim thay membership.", StatusCodes.Status404NotFound);
        }

        if (membership.Status != MembershipStatus.PendingPayment)
        {
            return Fail<CreateVnPayPaymentResponse>("INVALID_MEMBERSHIP_STATE", "Membership nay khong o trang thai cho thanh toan.", StatusCodes.Status409Conflict);
        }

        var profile = await _dbContext.MemberProfiles
            .FirstOrDefaultAsync(item => item.Id == membership.MemberId, cancellationToken);

        if (profile is null)
        {
            return Fail<CreateVnPayPaymentResponse>("NOT_FOUND", "Khong tim thay hoi vien.", StatusCodes.Status404NotFound);
        }

        if (!CanInitiate(principal, profile, actorId.Value))
        {
            return Fail<CreateVnPayPaymentResponse>("FORBIDDEN", "Ban khong co quyen thanh toan goi nay.", StatusCodes.Status403Forbidden);
        }

        // Tai dung payment Pending cua membership neu da co (vi du member bam thanh toan nhieu lan),
        // tranh tao rac. Neu chua co thi tao moi.
        var payment = await _dbContext.Payments
            .FirstOrDefaultAsync(
                item => item.MembershipId == membership.Id && item.Status == PaymentStatus.Pending,
                cancellationToken);

        if (payment is null)
        {
            payment = new Payment
            {
                MembershipId = membership.Id,
                Amount = membership.Package.Price,
                PaymentMethod = PaymentMethod.Transfer,
                Status = PaymentStatus.Pending,
                CreatedByUserId = actorId.Value,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Payments.Add(payment);
        }
        else
        {
            payment.Amount = membership.Package.Price;
            payment.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var payUrl = BuildPayUrl(payment, membership, ipAddress);

        return ServiceResult<CreateVnPayPaymentResponse>.Success(
            new CreateVnPayPaymentResponse(payment.Id, membership.Id, payment.Amount, payUrl),
            StatusCodes.Status201Created);
    }

    public async Task<VnPayIpnResponse> HandleIpnAsync(
        IReadOnlyDictionary<string, string> query,
        CancellationToken cancellationToken)
    {
        if (!TryVerify(query, out var inputHash) || !ValidateSignature(query, inputHash))
        {
            return new VnPayIpnResponse("97", "Invalid signature");
        }

        if (!TryGetPaymentId(query, out var paymentId))
        {
            return new VnPayIpnResponse("01", "Order not found");
        }

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(item => item.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            return new VnPayIpnResponse("01", "Order not found");
        }

        if (!IsAmountMatched(query, payment))
        {
            return new VnPayIpnResponse("04", "Invalid amount");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return new VnPayIpnResponse("02", "Order already confirmed");
        }

        if (IsSuccess(query))
        {
            var finalize = await FinalizeSuccessfulPaymentAsync(payment, cancellationToken);
            if (!finalize.Succeeded)
            {
                return new VnPayIpnResponse("99", finalize.ErrorCode ?? "Payment blocked");
            }
        }

        // Theo chuan VNPay: van ack "00" du giao dich that bai (chi khong kich hoat membership).
        return new VnPayIpnResponse("00", "Confirm Success");
    }

    public async Task<ServiceResult<VnPayPaymentStatusResponse>> HandleReturnAsync(
        IReadOnlyDictionary<string, string> query,
        CancellationToken cancellationToken)
    {
        if (!TryVerify(query, out var inputHash) || !ValidateSignature(query, inputHash))
        {
            return Fail<VnPayPaymentStatusResponse>("INVALID_SIGNATURE", "Chu ky khong hop le.", StatusCodes.Status400BadRequest);
        }

        if (!TryGetPaymentId(query, out var paymentId))
        {
            return Fail<VnPayPaymentStatusResponse>("NOT_FOUND", "Khong tim thay giao dich.", StatusCodes.Status404NotFound);
        }

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(item => item.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            return Fail<VnPayPaymentStatusResponse>("NOT_FOUND", "Khong tim thay giao dich.", StatusCodes.Status404NotFound);
        }

        if (!IsAmountMatched(query, payment))
        {
            return Fail<VnPayPaymentStatusResponse>("INVALID_AMOUNT", "So tien khong khop.", StatusCodes.Status400BadRequest);
        }

        // Fallback an toan cho demo: neu IPN chua kip toi (vi du local khong co tunnel),
        // return da verify chu ky + dung so tien thi van finalize duoc. Idempotent voi IPN.
        if (IsSuccess(query))
        {
            var finalize = await FinalizeSuccessfulPaymentAsync(payment, cancellationToken);
            if (!finalize.Succeeded)
            {
                return Fail<VnPayPaymentStatusResponse>(
                    finalize.ErrorCode ?? "PAYMENT_BLOCKED",
                    finalize.ErrorMessage ?? "Khong the hoan tat thanh toan.",
                    finalize.StatusCode);
            }
        }

        var membership = await _dbContext.Memberships.FirstOrDefaultAsync(item => item.Id == payment.MembershipId, cancellationToken);

        return ServiceResult<VnPayPaymentStatusResponse>.Success(
            new VnPayPaymentStatusResponse(
                payment.Id,
                payment.MembershipId,
                payment.Status.ToString(),
                membership?.Status.ToString() ?? "Unknown",
                payment.PaidAt));
    }

    // Kich hoat membership khi thanh toan thanh cong — idempotent (goi nhieu lan an toan).
    private async Task<ServiceResult<bool>> FinalizeSuccessfulPaymentAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (payment.Status == PaymentStatus.Paid)
        {
            return ServiceResult<bool>.Success(false);
        }

        var membership = await _dbContext.Memberships
            .Include(item => item.Package)
            .FirstOrDefaultAsync(item => item.Id == payment.MembershipId, cancellationToken);

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        if (membership is not null && membership.Status == MembershipStatus.PendingPayment)
        {
            // Het han goi Active qua han cua member, roi kich hoat don moi theo co che noi han.
            var todayVn = AppClock.Today();
            var others = await _dbContext.Memberships
                .Where(item => item.MemberId == membership.MemberId && item.Id != membership.Id && item.Status == MembershipStatus.Active)
                .ToListAsync(cancellationToken);
            foreach (var stale in others.Where(item => item.EndDate < todayVn))
            {
                stale.Status = MembershipStatus.Expired;
                stale.UpdatedAt = DateTime.UtcNow;
            }

            var replacedActive = MembershipLifecycle.ApplyPaidRenewalWindow(membership, others, todayVn);
            await MembershipActivation.CancelSiblingPendingAsync(_dbContext, membership.MemberId, membership.Id, cancellationToken);
            await MembershipActivation.SaveActivationAsync(_dbContext, membership, replacedActive, cancellationToken);
            await LogVnPayPaymentAsync(payment, cancellationToken);
            return ServiceResult<bool>.Success(true);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogVnPayPaymentAsync(payment, cancellationToken);
        return ServiceResult<bool>.Success(true);
    }

    private async Task LogVnPayPaymentAsync(Payment payment, CancellationToken cancellationToken)
    {
        await _auditService.LogAsync(
            "VNPAY_PAYMENT",
            "Payment",
            payment.Id,
            new { membershipId = payment.MembershipId },
            cancellationToken);
    }

    private string BuildPayUrl(Payment payment, Membership membership, string ipAddress)
    {
        var nowVn = DateTime.UtcNow.AddHours(7); // gio Viet Nam (GMT+7)
        var amount = ((long)(payment.Amount * 100)).ToString(CultureInfo.InvariantCulture);

        var library = new VnPayLibrary();
        library.AddRequestData("vnp_Version", _options.Version);
        library.AddRequestData("vnp_Command", _options.Command);
        library.AddRequestData("vnp_TmnCode", _options.TmnCode);
        library.AddRequestData("vnp_Amount", amount);
        library.AddRequestData("vnp_CurrCode", _options.CurrCode);
        library.AddRequestData("vnp_TxnRef", CreateTxnRef(payment.Id, nowVn));
        library.AddRequestData("vnp_OrderInfo", $"Thanh toan goi tap membership {membership.Id}");
        library.AddRequestData("vnp_OrderType", "other");
        library.AddRequestData("vnp_Locale", _options.Locale);
        library.AddRequestData("vnp_ReturnUrl", _options.ReturnUrl);
        // VNPay sandbox khong nhan IPv6 (vd "::1" khi chay local) -> ep ve IPv4.
        var ipv4 = string.IsNullOrWhiteSpace(ipAddress) || ipAddress.Contains(':') ? "127.0.0.1" : ipAddress;
        library.AddRequestData("vnp_IpAddr", ipv4);
        library.AddRequestData("vnp_CreateDate", nowVn.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
        library.AddRequestData("vnp_ExpireDate", nowVn.AddMinutes(15).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));

        return library.CreateRequestUrl(_options.BaseUrl, _options.HashSecret);
    }

    private bool ValidateSignature(IReadOnlyDictionary<string, string> query, string inputHash)
    {
        var library = new VnPayLibrary();
        foreach (var (key, value) in query)
        {
            if (key.StartsWith("vnp_", StringComparison.Ordinal) &&
                key != "vnp_SecureHash" &&
                key != "vnp_SecureHashType")
            {
                library.AddResponseData(key, value);
            }
        }

        return library.ValidateSignature(inputHash, _options.HashSecret);
    }

    private static bool TryVerify(IReadOnlyDictionary<string, string> query, out string inputHash)
    {
        return query.TryGetValue("vnp_SecureHash", out inputHash!) && !string.IsNullOrEmpty(inputHash);
    }

    private static bool TryGetPaymentId(IReadOnlyDictionary<string, string> query, out long paymentId)
    {
        paymentId = 0;
        if (!query.TryGetValue("vnp_TxnRef", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out paymentId))
        {
            return true;
        }

        if (!raw.StartsWith("GM", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separatorIndex = raw.IndexOf('T', 2);
        if (separatorIndex <= 2)
        {
            return false;
        }

        var idPart = raw[2..separatorIndex];
        return long.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out paymentId);
    }

    private static string CreateTxnRef(long paymentId, DateTime nowVn)
    {
        return $"GM{paymentId.ToString(CultureInfo.InvariantCulture)}T{nowVn:yyyyMMddHHmmssfff}";
    }

    private static bool IsAmountMatched(IReadOnlyDictionary<string, string> query, Payment payment)
    {
        if (!query.TryGetValue("vnp_Amount", out var raw) ||
            !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
        {
            return false;
        }

        return amount == (long)(payment.Amount * 100);
    }

    private static bool IsSuccess(IReadOnlyDictionary<string, string> query)
    {
        return query.TryGetValue("vnp_ResponseCode", out var responseCode) && responseCode == "00" &&
            query.TryGetValue("vnp_TransactionStatus", out var status) && status == "00";
    }

    private static bool CanInitiate(ClaimsPrincipal principal, MemberProfile profile, long actorId)
    {
        if (principal.IsInRole(RoleNames.Admin) || principal.IsInRole(RoleNames.Staff))
        {
            return true;
        }

        if (principal.IsInRole(RoleNames.Member))
        {
            return actorId == profile.UserId;
        }

        return false;
    }

    private static long? GetActorId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
            principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(value, out var userId) ? userId : null;
    }

    private static ServiceResult<T> Fail<T>(string code, string message, int statusCode)
    {
        return ServiceResult<T>.Failure(code, message, statusCode);
    }
}
