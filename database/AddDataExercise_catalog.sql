USE GymMasterDb;
GO

-- Thêm dữ liệu bài tập mẫu vào bảng exercise_catalog
INSERT INTO dbo.exercise_catalog (Name, MuscleGroup, Description, IsActive) 
VALUES 
(N'Squat (Gánh đùi)', N'Legs (Cơ đùi)', N'Bài tập vua cho vòng 3 và đùi trước', 1),
(N'Bench Press (Đẩy ngực)', N'Chest (Cơ ngực)', N'Bài tập phát triển cơ ngực toàn diện', 1),
(N'Deadlift', N'Back (Cơ lưng)', N'Bài tập phức hợp phát triển toàn thân', 1),
(N'Plank', N'Core (Cơ bụng)', N'Bài tập giữ người ổn định cơ trung tâm', 1);
GO