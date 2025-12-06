using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;

namespace kursovaya
{
    public static class DbProcedures
    {
        // Универсальный вызов selectable-proc (возвращает набор строк)
        public static DataTable CallSelectableProc(string dbPath, string procName, params FbParameter[] parameters)
        {
            using var conn = FirebirdDb.CreateConnection(dbPath);
            using var cmd = conn.CreateCommand();

            if (parameters != null && parameters.Length > 0)
            {
                var placeholders = string.Join(", ", parameters.Select(p => "@" + p.ParameterName.TrimStart('@')));
                cmd.CommandText = $"SELECT * FROM {procName}({placeholders})";
                cmd.Parameters.AddRange(parameters);
            }
            else
            {
                cmd.CommandText = $"SELECT * FROM {procName}";
            }

            var dt = new DataTable();
            conn.Open();
            using var adapter = new FbDataAdapter(cmd);
            adapter.Fill(dt);
            return dt;
        }

        // Универсальный вызов executable-proc (EXECUTE PROCEDURE) — возвращает одну строку/результат
        public static DataTable CallExecutableProc(string dbPath, string procName, params FbParameter[] parameters)
        {
            using var conn = FirebirdDb.CreateConnection(dbPath);
            using var cmd = conn.CreateCommand();

            if (parameters != null && parameters.Length > 0)
            {
                var placeholders = string.Join(", ", parameters.Select(p => "@" + p.ParameterName.TrimStart('@')));
                cmd.CommandText = $"EXECUTE PROCEDURE {procName}({placeholders})";
                cmd.Parameters.AddRange(parameters);
            }
            else
            {
                cmd.CommandText = $"EXECUTE PROCEDURE {procName}";
            }

            var dt = new DataTable();
            conn.Open();
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        // Пример: вызов ADD_STUDENT -> возвращает NEW_STUDENT_ID
        public static int AddStudent(string dbPath,
            string lastName, string firstName, string middleName,
            char gender, DateTime? birthDate, int yearAdmission, int classId,
            string phone, string email)
        {
            var parameters = new[]
            {
                new FbParameter("P_LASTNAME", FbDbType.VarChar) { Value = lastName ?? (object)DBNull.Value },
                new FbParameter("P_FIRSTNAME", FbDbType.VarChar) { Value = firstName ?? (object)DBNull.Value },
                new FbParameter("P_MIDDLENAME", FbDbType.VarChar) { Value = middleName ?? (object)DBNull.Value },
                new FbParameter("P_GENDER", FbDbType.Char) { Value = gender.ToString() },
                new FbParameter("P_BIRTHDATE", FbDbType.Date) { Value = birthDate ?? (object)DBNull.Value },
                new FbParameter("P_YEAR_ADMISSION", FbDbType.Integer) { Value = yearAdmission },
                new FbParameter("P_CLASS_ID", FbDbType.Integer) { Value = classId },
                new FbParameter("P_PHONE", FbDbType.VarChar) { Value = phone ?? (object)DBNull.Value },
                new FbParameter("P_EMAIL", FbDbType.VarChar) { Value = email ?? (object)DBNull.Value }
            };

            var dt = CallExecutableProc(dbPath, "ADD_STUDENT", parameters);
            if (dt.Rows.Count == 0) throw new InvalidOperationException("Процедура не вернула ожидаемый идентификатор.");
            return Convert.ToInt32(dt.Rows[0][0]);
        }

        // Пример: вызов ADD_ATTENDANCE -> возвращает NEW_ATTENDANCE_ID
        public static int AddAttendance(string dbPath, int studentId, int subjectId, DateTime lessonDate, char presence)
        {
            var parameters = new[]
            {
                new FbParameter("P_STUDENT_ID", FbDbType.Integer) { Value = studentId },
                new FbParameter("P_SUBJECT_ID", FbDbType.Integer) { Value = subjectId },
                new FbParameter("P_LESSON_DATE", FbDbType.Date) { Value = lessonDate },
                new FbParameter("P_PRESENCE", FbDbType.Char) { Value = presence.ToString() }
            };

            var dt = CallExecutableProc(dbPath, "ADD_ATTENDANCE", parameters);
            if (dt.Rows.Count == 0) throw new InvalidOperationException("Процедура не вернула ожидаемый идентификатор.");
            return Convert.ToInt32(dt.Rows[0][0]);
        }

        // Замените существующий метод AddAchievement на этот
        public static int AddAchievement(string dbPath, int studentId, string title, DateTime? eventDate, string level, string description)
        {
            var parameters = new[]
            {
                new FbParameter("P_STUDENT_ID", FbDbType.Integer) { Value = studentId },
                new FbParameter("P_TITLE", FbDbType.VarChar) { Value = title ?? (object)DBNull.Value },
                new FbParameter("P_EVENT_DATE", FbDbType.Date) { Value = eventDate ?? (object)DBNull.Value },
                new FbParameter("P_LEVEL", FbDbType.VarChar) { Value = level ?? (object)DBNull.Value },
                new FbParameter("P_DESCRIPTION", FbDbType.VarChar) { Value = description ?? (object)DBNull.Value }
            };

            var dt = CallExecutableProc(dbPath, "ADD_ACHIEVEMENT", parameters);
            if (dt.Rows.Count == 0) throw new InvalidOperationException("Процедура не вернула ожидаемый идентификатор.");
            return Convert.ToInt32(dt.Rows[0][0]);
        }

        // Замените существующий метод AddGrade на этот
        public static int AddGrade(string dbPath, int studentId, int subjectId, int grade, DateTime gradeDate)
        {
            var parameters = new[]
            {
                new FbParameter("P_STUDENT_ID", FbDbType.Integer) { Value = studentId },
                new FbParameter("P_SUBJECT_ID", FbDbType.Integer) { Value = subjectId },
                new FbParameter("P_GRADE", FbDbType.Integer) { Value = grade },
                new FbParameter("P_GRADE_DATE", FbDbType.Date) { Value = gradeDate }
            };

            var dt = CallExecutableProc(dbPath, "ADD_GRADE", parameters);
            if (dt.Rows.Count == 0) throw new InvalidOperationException("Процедура не вернула ожидаемый идентификатор.");
            return Convert.ToInt32(dt.Rows[0][0]);
        }

        // Пример: вызов GET_ALL_STUDENTS (selectable)
        public static DataTable GetAllStudents(string dbPath)
        {
            return CallSelectableProc(dbPath, "GET_ALL_STUDENTS");
        }

        // Добавить этот метод в класс DbProcedures
        public static int AddTeacher(string dbPath,
            string lastName, string firstName, string middleName,
            char gender, string jobTitle, string phone)
        {
            var parameters = new[]
            {
                new FbParameter("P_LASTNAME", FbDbType.VarChar) { Value = lastName ?? (object)DBNull.Value },
                new FbParameter("P_FIRSTNAME", FbDbType.VarChar) { Value = firstName ?? (object)DBNull.Value },
                new FbParameter("P_MIDDLENAME", FbDbType.VarChar) { Value = middleName ?? (object)DBNull.Value },
                new FbParameter("P_GENDER", FbDbType.Char) { Value = gender.ToString() },
                new FbParameter("P_JOB_TITLE", FbDbType.VarChar) { Value = jobTitle ?? (object)DBNull.Value },
                new FbParameter("P_PHONE", FbDbType.VarChar) { Value = phone ?? (object)DBNull.Value }
            };

            var dt = CallExecutableProc(dbPath, "ADD_TEACHER", parameters);
            if (dt.Rows.Count == 0) throw new InvalidOperationException("Процедура не вернула ожидаемый идентификатор.");
            return Convert.ToInt32(dt.Rows[0][0]);
        }

        // Добавляем новый метод AddClass
        public static int AddClass(string dbPath, string className, int? classTeacherId)
        {
            var parameters = new[]
            {
                new FbParameter("P_CLASS_NAME", FbDbType.VarChar) { Value = className ?? (object)DBNull.Value },
                new FbParameter("P_CLASS_TEACHER_ID", FbDbType.Integer) { Value = classTeacherId ?? (object)DBNull.Value }
            };

            var dt = CallExecutableProc(dbPath, "ADD_CLASS", parameters);
            if (dt.Rows.Count == 0) throw new InvalidOperationException("Процедура не вернула ожидаемый идентификатор.");
            return Convert.ToInt32(dt.Rows[0][0]);
        }

        // BLOB helpers: Image <-> byte[]
        public static byte[]? ImageToBlob(Image? img)
        {
            if (img == null) return null;
            using var ms = new MemoryStream();
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ms.ToArray();
        }

        public static Image? BlobToImage(byte[]? blob)
        {
            if (blob == null || blob.Length == 0) return null;
            using var ms = new MemoryStream(blob);
            using var src = Image.FromStream(ms);
            // Возвращаем независимый Bitmap, чтобы не зависеть от закрытого MemoryStream
            return new Bitmap(src);
        }

        // Добавлены: выполнение процедур без результата и обёртки для каскадного удаления
        public static void CallNonQueryProc(string dbPath, string procName, params FbParameter[] parameters)
        {
            using var conn = FirebirdDb.CreateConnection(dbPath);
            using var cmd = conn.CreateCommand();

            if (parameters != null && parameters.Length > 0)
            {
                var placeholders = string.Join(", ", parameters.Select(p => "@" + p.ParameterName.TrimStart('@')));
                cmd.CommandText = $"EXECUTE PROCEDURE {procName}({placeholders})";
                cmd.Parameters.AddRange(parameters);
            }
            else
            {
                cmd.CommandText = $"EXECUTE PROCEDURE {procName}";
            }

            conn.Open();
            cmd.ExecuteNonQuery();
        }

        public static void DeleteStudentCascade(string dbPath, int studentId)
        {
            if (studentId <= 0) throw new ArgumentException("studentId должен быть положительным.", nameof(studentId));

            var parameters = new[]
            {
                new FbParameter("P_STUDENT_ID", FbDbType.Integer) { Value = studentId }
            };

            CallNonQueryProc(dbPath, "DELETE_STUDENT_CASCADE", parameters);
        }

        public static void DeleteTeacherCascade(string dbPath, int teacherId)
        {
            if (teacherId <= 0) throw new ArgumentException("teacherId должен быть положительным.", nameof(teacherId));

            var parameters = new[]
            {
                new FbParameter("P_TEACHER_ID", FbDbType.Integer) { Value = teacherId }
            };

            CallNonQueryProc(dbPath, "DELETE_TEACHER_CASCADE", parameters);
        }

        // Обёртки для обновлений через процедуры UPDATE_...
        public static void UpdateStudent(string dbPath, int studentId,
            string lastName, string firstName, string? middleName,
            char gender, DateTime? birthDate, int yearAdmission, int classId,
            string? phone, string? email)
        {
            var parameters = new[]
            {
                new FbParameter("P_STUDENT_ID", FbDbType.Integer) { Value = studentId },
                new FbParameter("P_LASTNAME", FbDbType.VarChar) { Value = lastName ?? (object)DBNull.Value },
                new FbParameter("P_FIRSTNAME", FbDbType.VarChar) { Value = firstName ?? (object)DBNull.Value },
                new FbParameter("P_MIDDLENAME", FbDbType.VarChar) { Value = middleName ?? (object)DBNull.Value },
                new FbParameter("P_GENDER", FbDbType.Char) { Value = gender.ToString() },
                new FbParameter("P_BIRTHDATE", FbDbType.Date) { Value = birthDate ?? (object)DBNull.Value },
                new FbParameter("P_YEAR_ADMISSION", FbDbType.Integer) { Value = yearAdmission },
                new FbParameter("P_CLASS_ID", FbDbType.Integer) { Value = classId },
                new FbParameter("P_PHONE", FbDbType.VarChar) { Value = phone ?? (object)DBNull.Value },
                new FbParameter("P_EMAIL", FbDbType.VarChar) { Value = email ?? (object)DBNull.Value }
            };
            CallNonQueryProc(dbPath, "UPDATE_STUDENT", parameters);
        }

        public static void UpdateTeacher(string dbPath, int teacherId,
            string lastName, string firstName, string? middleName,
            char gender, string jobTitle, string? phone)
        {
            var parameters = new[]
            {
                new FbParameter("P_TEACHER_ID", FbDbType.Integer) { Value = teacherId },
                new FbParameter("P_LASTNAME", FbDbType.VarChar) { Value = lastName ?? (object)DBNull.Value },
                new FbParameter("P_FIRSTNAME", FbDbType.VarChar) { Value = firstName ?? (object)DBNull.Value },
                new FbParameter("P_MIDDLENAME", FbDbType.VarChar) { Value = middleName ?? (object)DBNull.Value },
                new FbParameter("P_GENDER", FbDbType.Char) { Value = gender.ToString() },
                new FbParameter("P_JOB_TITLE", FbDbType.VarChar) { Value = jobTitle ?? (object)DBNull.Value },
                new FbParameter("P_PHONE", FbDbType.VarChar) { Value = phone ?? (object)DBNull.Value }
            };
            CallNonQueryProc(dbPath, "UPDATE_TEACHER", parameters);
        }

        public static void UpdateAchievement(string dbPath, int achievementId,
            string title, DateTime? eventDate, string? level, string? description)
        {
            var parameters = new[]
            {
                new FbParameter("P_ACHIEVEMENT_ID", FbDbType.Integer) { Value = achievementId },
                new FbParameter("P_TITLE", FbDbType.VarChar) { Value = title ?? (object)DBNull.Value },
                new FbParameter("P_EVENT_DATE", FbDbType.Date) { Value = eventDate ?? (object)DBNull.Value },
                new FbParameter("P_LEVEL", FbDbType.VarChar) { Value = level ?? (object)DBNull.Value },
                new FbParameter("P_DESCRIPTION", FbDbType.VarChar) { Value = description ?? (object)DBNull.Value }
            };
            CallNonQueryProc(dbPath, "UPDATE_ACHIEVEMENT", parameters);
        }

        public static void UpdateCurriculum(string dbPath, int classId, int subjectId, int teacherId, int hours)
        {
            var parameters = new[]
            {
                new FbParameter("P_CLASS_ID", FbDbType.Integer) { Value = classId },
                new FbParameter("P_SUBJECT_ID", FbDbType.Integer) { Value = subjectId },
                new FbParameter("P_TEACHER_ID", FbDbType.Integer) { Value = teacherId },
                new FbParameter("P_HOURS", FbDbType.Integer) { Value = hours }
            };
            CallNonQueryProc(dbPath, "UPDATE_CURRICULUM", parameters);
        }

        public static void UpdateGrade(string dbPath, int gradeId, int grade, DateTime gradeDate)
        {
            var parameters = new[]
            {
                new FbParameter("P_GRADE_ID", FbDbType.Integer) { Value = gradeId },
                new FbParameter("P_GRADE", FbDbType.Integer) { Value = grade },
                new FbParameter("P_GRADE_DATE", FbDbType.Date) { Value = gradeDate }
            };
            CallNonQueryProc(dbPath, "UPDATE_GRADE", parameters);
        }

        public static void UpdateAttendance(string dbPath, int attendanceId, char presence, DateTime lessonDate)
        {
            var parameters = new[]
            {
                new FbParameter("P_ATTENDANCE_ID", FbDbType.Integer) { Value = attendanceId },
                new FbParameter("P_PRESENCE", FbDbType.Char) { Value = presence.ToString() },
                new FbParameter("P_LESSON_DATE", FbDbType.Date) { Value = lessonDate }
            };
            CallNonQueryProc(dbPath, "UPDATE_ATTENDANCE", parameters);
        }

        // BLOB updates (direct SQL) — для фото
        public static void UpdateStudentPhoto(string dbPath, int studentId, byte[] photo)
        {
            var param = new FbParameter("P_PHOTO", FbDbType.Binary) { Value = photo ?? (object)DBNull.Value };
            var id = new FbParameter("P_STUDENT_ID", FbDbType.Integer) { Value = studentId };
            global::kursovaya.FirebirdDb.ExecuteNonQuery(dbPath, "UPDATE STUDENTS SET PHOTO = @P_PHOTO WHERE STUDENT_ID = @P_STUDENT_ID", param, id);
        }

        public static void UpdateTeacherPhoto(string dbPath, int teacherId, byte[] photo)
        {
            var param = new FbParameter("P_PHOTO", FbDbType.Binary) { Value = photo ?? (object)DBNull.Value };
            var id = new FbParameter("P_TEACHER_ID", FbDbType.Integer) { Value = teacherId };
            global::kursovaya.FirebirdDb.ExecuteNonQuery(dbPath, "UPDATE TEACHERS SET PHOTO = @P_PHOTO WHERE TEACHER_ID = @P_TEACHER_ID", param, id);
        }

        public static void UpdateAchievementPhoto(string dbPath, int achievementId, byte[] photo)
        {
            var param = new FbParameter("P_PHOTO", FbDbType.Binary) { Value = photo ?? (object)DBNull.Value };
            var id = new FbParameter("P_ACHIEVEMENT_ID", FbDbType.Integer) { Value = achievementId };
            global::kursovaya.FirebirdDb.ExecuteNonQuery(dbPath, "UPDATE ACHIEVEMENTS SET PROOF_PHOTO = @P_PHOTO WHERE ACHIEVEMENT_ID = @P_ACHIEVEMENT_ID", param, id);
        }
    }
}
