using BlazorOrderApp.Models;
using Dapper;
using Npgsql;
using System.Data;

namespace BlazorOrderApp.Repositories
{
    public interface IOrderRepository
    {
        Task<IEnumerable<OrderModel>> GetAllAsync();
        Task<IEnumerable<OrderModel>> SearchAsync(DateTime startDate, DateTime endDate, string keyword);
        Task<OrderModel?> GetByIdAsync(int? 受注ID);
        Task<IEnumerable<OrderModel>> GetHistoryAsync(DateTime? startDate, DateTime? endDate, string? keyword);
        Task AddAsync(OrderModel model);
        Task UpdateAsync(OrderModel model);
        Task DeleteAsync(OrderModel model);
    }

    public class OrderRepository : IOrderRepository
    {
        private readonly string _connectionString;

        public OrderRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        // 全件
        public async Task<IEnumerable<OrderModel>> GetAllAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);

            var dataSql = @"
                select ""受注ID"", ""受注日"", ""得意先ID"", ""得意先名"", ""合計金額"", ""備考"", ""Version""
                  from ""受注""
                 order by ""受注日"", ""得意先名"", ""受注ID""
            ";
            var list = await conn.QueryAsync<OrderModel>(dataSql);

            return list;
        }

        // 検索
        public async Task<IEnumerable<OrderModel>> SearchAsync(DateTime startDate, DateTime endDate, string keyword)
        {
            using var conn = new NpgsqlConnection(_connectionString);

            var dataSql = @"
                 with sub as (
                    select o.""受注ID"", o.""受注日"", o.""得意先ID"", o.""得意先名"", o.""合計金額"", o.""備考"", o.""Version""
                      from ""受注"" o
                     where o.""受注日"" between @startDate and @endDate
                )
                select *
                from sub t
                where
                      @isEmpty
                   or t.""得意先名"" ilike @key
                   or exists (
                         select 1 from ""受注明細"" d
                          where d.""受注ID"" = t.""受注ID""
                            and (d.""商品コード"" ilike @key or d.""商品名"" ilike @key)
                     )
                order by t.""受注日"", t.""得意先名"", t.""受注ID""
           ";
            var param = new
            {
                startDate,
                endDate,
                key = "%" + keyword + "%",
                isEmpty = string.IsNullOrWhiteSpace(keyword)
            };
            var list = await conn.QueryAsync<OrderModel>(dataSql, param);
            return list;
        }

        // 履歴（明細含む）
        public async Task<IEnumerable<OrderModel>> GetHistoryAsync(DateTime? startDate, DateTime? endDate, string? keyword)
        {
            using var conn = new NpgsqlConnection(_connectionString);

            var safeStart = startDate ?? new DateTime(1900, 1, 1);
            var safeEnd = endDate ?? new DateTime(2999, 12, 31);
            var key = (keyword ?? string.Empty).Trim();

            var sql = @"
                select
                    o.""受注ID"",
                    o.""受注日"",
                    o.""得意先ID"",
                    o.""得意先名"",
                    o.""合計金額"",
                    o.""備考"",
                    o.""Version"",
                    d.""明細ID"",
                    d.""受注ID"",
                    d.""商品コード"",
                    d.""商品名"",
                    d.""単価"",
                    d.""数量""
                from ""受注"" o
                left join ""受注明細"" d on d.""受注ID"" = o.""受注ID""
                where o.""受注日"" between @startDate and @endDate
                  and (
                        @isEmpty
                     or o.""得意先名"" ilike @key
                     or d.""商品コード"" ilike @key
                     or d.""商品名"" ilike @key
                  )
                order by o.""受注日"" desc, o.""受注ID"" desc, d.""明細ID""
            ";

            var lookup = new Dictionary<int, OrderModel>();
            await conn.QueryAsync<OrderModel, OrderDetailModel, OrderModel>(
                sql,
                (order, detail) =>
                {
                    if (!lookup.TryGetValue(order.受注ID, out var existing))
                    {
                        existing = order;
                        existing.明細一覧 = new List<OrderDetailModel>();
                        lookup.Add(existing.受注ID, existing);
                    }

                    if (detail != null && detail.明細ID != 0)
                    {
                        // Dapper の Split で別名列がマッピングされないため受注IDを合わせる
                        detail.受注ID = existing.受注ID;
                        existing.明細一覧.Add(detail);
                    }

                    return existing;
                },
                param: new
                {
                    startDate = safeStart,
                    endDate = safeEnd,
                    key = "%" + key + "%",
                    isEmpty = string.IsNullOrWhiteSpace(key)
                },
                splitOn: "明細ID"
            );

            return lookup.Values
                         .OrderByDescending(o => o.受注日)
                         .ThenByDescending(o => o.受注ID)
                         .ToList();
        }

        // 単一 Select
        public async Task<OrderModel?> GetByIdAsync(int? 受注ID)
        {
            if (受注ID == null) return null;

            using var conn = new NpgsqlConnection(_connectionString);

            var sql = @"
                select o.""受注ID"", o.""受注日"", o.""得意先ID"", o.""得意先名"", o.""合計金額"", o.""備考"", o.""Version"",
                       d.""明細ID"", d.""受注ID"", d.""商品コード"", d.""商品名"", d.""単価"", d.""数量""
                  from ""受注"" o
                  left join ""受注明細"" d on o.""受注ID"" = d.""受注ID""
                 where o.""受注ID"" = @受注ID
            ";

            // １：Ｎのデータ構造を１ＳＱＬで取得する
            var lookup = new Dictionary<int, OrderModel>();
            await conn.QueryAsync<OrderModel, OrderDetailModel, OrderModel>(
                sql,
                (o, d) =>
                {
                    if (!lookup.TryGetValue(o.受注ID, out var order))
                    {
                        order = o;
                        order.明細一覧 = new List<OrderDetailModel>();
                        lookup.Add(order.受注ID, order);
                    }
                    if (d != null && d.明細ID != 0)
                    {
                        order.明細一覧.Add(d);
                    }
                    return order;
                },
                param: new { 受注ID },
                splitOn: "明細ID"
            );

            // 最初の1件（存在しなければnull）
            return lookup.Values.FirstOrDefault();
        }


        // Insert
        // OrderRepository.cs
        public async Task AddAsync(OrderModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                model.Version = 1;
                model.合計金額 = model.明細一覧.Sum(x => x.単価 * x.数量);

                // 受注テーブル
                var sql1 = @"
                    insert into ""受注"" (""受注日"", ""得意先ID"", ""得意先名"", ""合計金額"", ""備考"", ""Version"")
                    values (@受注日, @得意先ID, @得意先名, @合計金額, @備考, @Version)
                    returning ""受注ID"";
                ";
                model.受注ID = await conn.ExecuteScalarAsync<int>(sql1, model, tran);

                // 受注明細テーブル（商品コードが入力されているもののみ）
                var sql2 = @"
                    insert into ""受注明細"" (""受注ID"", ""商品コード"", ""商品名"", ""単価"", ""数量"")
                    values (@受注ID, @商品コード, @商品名, @単価, @数量)
                ";
                foreach (var 明細 in model.明細一覧.Where(m => !string.IsNullOrWhiteSpace(m.商品コード)))
                {
                    明細.受注ID = model.受注ID;
                    await conn.ExecuteAsync(sql2, 明細, tran);
                }

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        // Update
        public async Task UpdateAsync(OrderModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                var sql1 = @"
                    update ""受注"" set
                        ""受注日"" = @受注日,
                        ""得意先ID"" = @得意先ID,
                        ""得意先名"" = @得意先名,
                        ""合計金額"" = @合計金額,
                        ""備考"" = @備考,
                        ""Version"" = ""Version"" + 1
                    where ""受注ID"" = @受注ID
                      and ""Version"" = @Version
                ";
                var rows = await conn.ExecuteAsync(sql1, model, tran);
                if (rows == 0)
                {
                    throw new DBConcurrencyException("他で更新されています。再読み込みしてください。");
                }

                // 紐づく受注明細をいったん全て削除
                await conn.ExecuteAsync("delete from \"受注明細\" where \"受注ID\" = @受注ID", new { model.受注ID }, tran);

                // 新しい明細をINSERT（商品コードが入力されているもののみ）
                var sql2 = @"
                    insert into ""受注明細"" (""受注ID"", ""商品コード"", ""商品名"", ""単価"", ""数量"")
                    values (@受注ID, @商品コード, @商品名, @単価, @数量)
                ";
                foreach (var 明細 in model.明細一覧.Where(m => !string.IsNullOrWhiteSpace(m.商品コード)))
                {
                    明細.受注ID = model.受注ID;
                    await conn.ExecuteAsync(sql2, 明細, tran);
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
        public async Task DeleteAsync(OrderModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = conn.BeginTransaction();

            try
            {
                await conn.ExecuteAsync("delete from \"受注明細\" where \"受注ID\" = @受注ID", new { model.受注ID }, tran);
                var sql = @"
                    delete from ""受注""
                    where ""受注ID"" = @受注ID
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
