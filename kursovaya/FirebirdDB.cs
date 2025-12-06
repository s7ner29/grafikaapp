using System;
using System.Data;
using FirebirdSql.Data.FirebirdClient;

namespace kursovaya
{
    public static class FirebirdDb
    {
        public static FbConnection CreateConnection(string dbFilePath)
        {
            var csb = new FbConnectionStringBuilder
            {
                DataSource = "localhost",
                Port = 3050,
                Database = dbFilePath,
                UserID = "SYSDBA",
                Password = "masterkey",
                Charset = "NONE",
                Dialect = 3,
                Pooling = true
            };
            return new FbConnection(csb.ToString());
        }

        public static void EnsureAutoddl(FbConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SET AUTODDL ON;";
            cmd.ExecuteNonQuery();
        }

        public static DataTable ExecuteQuery(string dbFilePath, string sql, params FbParameter[] parameters)
        {
            using var conn = CreateConnection(dbFilePath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (parameters != null && parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            var dt = new DataTable();
            conn.Open();
            using var adapter = new FbDataAdapter(cmd);
            adapter.Fill(dt);
            return dt;
        }

        public static int ExecuteNonQuery(string dbFilePath, string sql, params FbParameter[] parameters)
        {
            using var conn = CreateConnection(dbFilePath);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (parameters != null && parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            conn.Open();
            return cmd.ExecuteNonQuery();
        }
    }
}
