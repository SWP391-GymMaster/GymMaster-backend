using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace GymMaster.API.Infrastructure;
// Helper ky/verify chu ky VNPay (chuan 2.1.0). Logic giong het sandbox va live —
// chuyen moi truong chi can doi BaseUrl + TmnCode + HashSecret o cau hinh.
//
// Quy tac: gom cac tham so vnp_*, sap xep theo key (ordinal), URL-encode value, noi bang '&',
// roi HMAC-SHA512 voi HashSecret => vnp_SecureHash (hex thuong).
public sealed class VnPayLibrary
{
    private readonly SortedDictionary<string, string> _requestData =
        new(StringComparer.Ordinal);

    private readonly SortedDictionary<string, string> _responseData =
        new(StringComparer.Ordinal);

    // Them tham so khi DUNG URL thanh toan (chieu di).
    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _requestData[key] = value;
        }
    }

    // Them tham so khi NHAN callback/IPN (chieu ve) de verify.
    public void AddResponseData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _responseData[key] = value;
        }
    }

    // Tao URL thanh toan day du (da gan vnp_SecureHash).
    public string CreateRequestUrl(string baseUrl, string hashSecret)
    {
        var query = BuildDataString(_requestData, encode: true);
        var signData = BuildDataString(_requestData, encode: true);
        var secureHash = HmacSha512(hashSecret, signData);
        return $"{baseUrl}?{query}&vnp_SecureHash={secureHash}";
    }

    // Verify chu ky tu callback/IPN. So sanh khong phan biet hoa thuong.
    public bool ValidateSignature(string inputHash, string hashSecret)
    {
        var data = BuildDataString(_responseData, encode: true);
        var computed = HmacSha512(hashSecret, data);
        return computed.Equals(inputHash, StringComparison.InvariantCultureIgnoreCase);
    }

    private static string BuildDataString(SortedDictionary<string, string> data, bool encode)
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in data)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            builder.Append(Encode(key, encode))
                .Append('=')
                .Append(Encode(value, encode));
        }

        return builder.ToString();
    }

    private static string Encode(string value, bool encode)
    {
        return encode ? WebUtility.UrlEncode(value) ?? string.Empty : value;
    }

    private static string HmacSha512(string key, string input)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
