using BlazorOrderApp.Models;
using Dapper;
using Npgsql;
using System.Data;

namespace BlazorOrderApp.Repositories
{
    public interface ICustomerRepository
    {
        Task<List<CustomerModel>> GetAllAsync();
        Task<CustomerModel?> GetByIdAsync(int? 得意先ID);
        Task AddAsync(CustomerModel model);
        Task UpdateAsync(CustomerModel model);
        Task DeleteAsync(CustomerModel model);
    }

    public class CustomerRepository : ICustomerRepository
    {
        private readonly string _connectionString;

        public CustomerRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        // 全件Select
        public async Task<List<CustomerModel>> GetAllAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);
            var dataSql = @"
                select ""得意先ID"", ""得意先名"", ""電話番号"", ""備考"", ""Version""
                  from ""得意先""
                 order by ""得意先名""
            ";
            var list = await conn.QueryAsync<CustomerModel>(dataSql);
            return list.ToList();
        }

        public async Task<CustomerModel?> GetByIdAsync(int? 得意先ID)
        {
            if (得意先ID == null) return null;

            using var conn = new NpgsqlConnection(_connectionString);

            var sql = @"
                select ""得意先ID"", ""得意先名"", ""電話番号"", ""備考"", ""Version""
                  from ""得意先""
                 where ""得意先ID"" = @得意先ID
            ";

            var item = await conn.QueryFirstOrDefaultAsync<CustomerModel>(sql, new { 得意先ID });
            return item;
        }

        // Insert
        public async Task AddAsync(CustomerModel model)
        {
            model.Version = 1;

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                var sql = @"
                    insert into ""得意先"" (""得意先名"", ""電話番号"", ""備考"", ""Version"")
                    values (@得意先名, @電話番号, @備考, @Version)
                ";
                await conn.ExecuteAsync(sql, model, tran);
                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        // Update
        public async Task UpdateAsync(CustomerModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                var sql = @"
                    update ""得意先"" set
                        ""得意先名"" = @得意先名,
                        ""電話番号"" = @電話番号,
                        ""備考"" = @備考,
                        ""Version"" = ""Version"" + 1
                    where ""得意先ID"" = @得意先ID
                      and ""Version"" = @Version
                ";
                var rows = await conn.ExecuteAsync(sql, model, tran);
                if (rows == 0)
                {
                    // 他ユーザーが既に更新済み
                    throw new DBConcurrencyException("他で更新されています。再読み込みしてください。");
                }
                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        // Delete
        public async Task DeleteAsync(CustomerModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                var sql = @"
                    delete from ""得意先""
                    where ""得意先ID"" = @得意先ID
                      and ""Version"" = @Version
                ";
                var rows = await conn.ExecuteAsync(sql, model, tran);
                if (rows == 0)
                {
                    // 他ユーザーが既に更新済み
                    throw new DBConcurrencyException("他で更新されています。再読み込みしてください。");
                }
                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }
    }
}
