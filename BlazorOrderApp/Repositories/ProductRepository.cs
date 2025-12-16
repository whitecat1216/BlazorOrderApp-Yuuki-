using BlazorOrderApp.Models;
using Dapper;
using Npgsql;
using System.Data;

namespace BlazorOrderApp.Repositories
{
    public interface IProductRepository
    {
        Task<List<ProductModel>> GetAllAsync();
        Task<List<ProductModel>> SearchAsync(string keyword);
        Task<ProductModel?> GetByCodeAsync(string 商品コード);
        Task AddAsync(ProductModel model);
        Task UpdateAsync(ProductModel model);
        Task DeleteAsync(ProductModel model);
    }

    public class ProductRepository : IProductRepository
    {
        private readonly string _connectionString;

        public ProductRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        // 全件Select
        public async Task<List<ProductModel>> GetAllAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);

            var dataSql = @"
                select ""商品コード"", ""商品名"", ""単価"", ""備考""
                  from ""商品""
                 order by ""商品コード""
            ";
            var list = await conn.QueryAsync<ProductModel>(dataSql);

            return list.ToList();
        }

        // 検索
        public async Task<List<ProductModel>> SearchAsync(string keyword)
        {
            using var conn = new NpgsqlConnection(_connectionString);

            var dataSql = @"
                select ""商品コード"", ""商品名"", ""単価"", ""備考"", ""Version""
                  from ""商品""
                where ( ""商品コード"" ilike @keyword or ""商品名"" ilike @keyword)
                 order by ""商品コード""
                limit 10
            ";
            var list = await conn.QueryAsync<ProductModel>(dataSql, new { keyword = $"%{keyword}%" });

            return list.ToList();
        }

        // 単一 Select
        public async Task<ProductModel?> GetByCodeAsync(string 商品コード)
        {
            using var conn = new NpgsqlConnection(_connectionString);

            var sql = @"
                select ""商品コード"", ""商品名"", ""単価"", ""備考"", ""Version""
                  from ""商品""
                 where ""商品コード"" = @商品コード
            ";

            var item = await conn.QueryFirstOrDefaultAsync<ProductModel>(sql, new { 商品コード });
            return item;
        }

        // Insert
        public async Task AddAsync(ProductModel model)
        {
            model.Version = 1;

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                var sql = @"
                    insert into ""商品"" (""商品コード"", ""商品名"", ""単価"", ""備考"", ""Version"")
                    values (@商品コード, @商品名, @単価, @備考, @Version)
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
        public async Task UpdateAsync(ProductModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                var sql = @"
                    update ""商品"" set
                        ""商品名"" = @商品名,
                        ""単価"" = @単価,
                        ""備考"" = @備考,
                        ""Version"" = ""Version"" + 1
                    where ""商品コード"" = @商品コード
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
        public async Task DeleteAsync(ProductModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                var sql = @"
                    delete from ""商品""
                    where ""商品コード"" = @商品コード
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
