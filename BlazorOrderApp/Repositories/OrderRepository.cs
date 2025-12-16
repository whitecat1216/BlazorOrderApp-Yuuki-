using BlazorOrderApp.Models;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace BlazorOrderApp.Repositories
{
    public interface IOrderRepository
    {
        Task<IEnumerable<OrderModel>> GetAllAsync();
        Task<IEnumerable<OrderModel>> SearchAsync(DateTime startDate, DateTime endDate, string keyword, string? sortColumn = null, string? sortDirection = null);
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

        public async Task<IEnumerable<OrderModel>> GetAllAsync()
        {
            using var conn = new NpgsqlConnection(_connectionString);

            const string sql = @"
                select ""受注ID"", ""受注日"", ""得意先ID"", ""得意先名"", ""合計金額"", ""備考"", ""Version""
                  from ""受注""
                 order by ""受注日"", ""得意先名"", ""受注ID""
            ";

            return await conn.QueryAsync<OrderModel>(sql);
        }

        public async Task<IEnumerable<OrderModel>> SearchAsync(DateTime startDate, DateTime endDate, string keyword, string? sortColumn = null, string? sortDirection = null)
        {
            using var conn = new NpgsqlConnection(_connectionString);

            var safeSortColumn = GetSafeColumnName(sortColumn);
            var safeSortDirection = string.Equals(sortDirection, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

            var sql = $@"
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
                 order by t.""{safeSortColumn}"" {safeSortDirection}, t.""受注ID""
            ";

            var trimmedKeyword = (keyword ?? string.Empty).Trim();

            var param = new
            {
                startDate,
                endDate,
                key = "%" + trimmedKeyword + "%",
                isEmpty = string.IsNullOrWhiteSpace(trimmedKeyword)
            };

            return await conn.QueryAsync<OrderModel>(sql, param);
        }

        public async Task<OrderModel?> GetByIdAsync(int? 受注ID)
        {
            if (受注ID == null)
            {
                return null;
            }

            using var conn = new NpgsqlConnection(_connectionString);

            const string sql = @"
                select o.""受注ID"", o.""受注日"", o.""得意先ID"", o.""得意先名"", o.""合計金額"", o.""備考"", o.""Version"",
                       d.""明細ID"", d.""受注ID"", d.""商品コード"", d.""商品名"", d.""単価"", d.""数量""
                  from ""受注"" o
                  left join ""受注明細"" d on o.""受注ID"" = d.""受注ID""
                 where o.""受注ID"" = @受注ID
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
                        detail.受注ID = existing.受注ID;
                        existing.明細一覧.Add(detail);
                    }

                    return existing;
                },
                param: new { 受注ID },
                splitOn: "明細ID"
            );

            return lookup.Values.FirstOrDefault();
        }

        public async Task<IEnumerable<OrderModel>> GetHistoryAsync(DateTime? startDate, DateTime? endDate, string? keyword)
        {
            using var conn = new NpgsqlConnection(_connectionString);

            var safeStart = startDate ?? new DateTime(1900, 1, 1);
            var safeEnd = endDate ?? new DateTime(2999, 12, 31);
            var key = (keyword ?? string.Empty).Trim();

            const string sql = @"
                select
                    o.""受注ID"", o.""受注日"", o.""得意先ID"", o.""得意先名"", o.""合計金額"", o.""備考"", o.""Version"",
                    d.""明細ID"", d.""受注ID"", d.""商品コード"", d.""商品名"", d.""単価"", d.""数量""
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

        public async Task AddAsync(OrderModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = await conn.BeginTransactionAsync();

            try
            {
                model.Version = 1;
                model.合計金額 = model.明細一覧.Sum(x => x.単価 * x.数量);

                const string insert受注 = @"
                    insert into ""受注"" (""受注日"", ""得意先ID"", ""得意先名"", ""合計金額"", ""備考"", ""Version"")
                    values (@受注日, @得意先ID, @得意先名, @合計金額, @備考, @Version)
                    returning ""受注ID"";
                ";

                model.受注ID = await conn.ExecuteScalarAsync<int>(insert受注, model, tran);

                const string insert明細 = @"
                    insert into ""受注明細"" (""受注ID"", ""商品コード"", ""商品名"", ""単価"", ""数量"")
                    values (@受注ID, @商品コード, @商品名, @単価, @数量)
                ";

                foreach (var 明細 in model.明細一覧.Where(m => !string.IsNullOrWhiteSpace(m.商品コード)))
                {
                    明細.受注ID = model.受注ID;
                    await conn.ExecuteAsync(insert明細, 明細, tran);
                }

                await tran.CommitAsync();
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateAsync(OrderModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = await conn.BeginTransactionAsync();

            try
            {
                const string update受注 = @"
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

                var rows = await conn.ExecuteAsync(update受注, model, tran);
                if (rows == 0)
                {
                    throw new DBConcurrencyException("他で更新されています。再読み込みしてください。");
                }

                await conn.ExecuteAsync("delete from \"受注明細\" where \"受注ID\" = @受注ID", new { model.受注ID }, tran);

                const string insert明細 = @"
                    insert into ""受注明細"" (""受注ID"", ""商品コード"", ""商品名"", ""単価"", ""数量"")
                    values (@受注ID, @商品コード, @商品名, @単価, @数量)
                ";

                foreach (var 明細 in model.明細一覧.Where(m => !string.IsNullOrWhiteSpace(m.商品コード)))
                {
                    明細.受注ID = model.受注ID;
                    await conn.ExecuteAsync(insert明細, 明細, tran);
                }

                await tran.CommitAsync();
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteAsync(OrderModel model)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tran = await conn.BeginTransactionAsync();

            try
            {
                await conn.ExecuteAsync("delete from \"受注明細\" where \"受注ID\" = @受注ID", new { model.受注ID }, tran);

                const string delete受注 = @"
                    delete from ""受注""
                     where ""受注ID"" = @受注ID
                       and ""Version"" = @Version
                ";

                var rows = await conn.ExecuteAsync(delete受注, model, tran);
                if (rows == 0)
                {
                    throw new DBConcurrencyException("他で更新されています。再読み込みしてください。");
                }

                await tran.CommitAsync();
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }

        private static string GetSafeColumnName(string? columnName)
        {
            var sortableColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "受注ID", "受注ID" },
                { "受注日", "受注日" },
                { "得意先名", "得意先名" },
                { "合計金額", "合計金額" }
            };

            if (columnName == null || !sortableColumns.TryGetValue(columnName, out var safeName))
            {
                return "受注日";
            }

            return safeName;
        }
    }
}