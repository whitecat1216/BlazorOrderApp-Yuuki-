using BlazorOrderApp.Models;
using Dapper;
using Npgsql;

namespace BlazorOrderApp.Repositories
{
    public interface IOrderAnalysisRepository
    {
        Task<IEnumerable<日別受注金額Model>> Get日別受注金額Async(DateTime startDate, DateTime endDate);
        Task<IEnumerable<日別受注金額Model>> Get週別受注金額Async(DateTime startDate, DateTime endDate);
        Task<IEnumerable<得意先別受注金額Model>> Get得意先別受注金額Async(DateTime startDate, DateTime endDate);
        Task<IEnumerable<商品別受注金額Model>> Get商品別受注金額Async(DateTime startDate, DateTime endDate);
    }

    public class OrderAnalysisRepository : IOrderAnalysisRepository
    {
        private readonly string _connectionString;

        public OrderAnalysisRepository(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<IEnumerable<日別受注金額Model>> Get日別受注金額Async(DateTime startDate, DateTime endDate)
        {
            using var conn = new NpgsqlConnection(_connectionString);

            // 動的日付リスト生成＆ゼロ補完
            var dataSql = @"
                with DateList(""受注日"") as (
                    select generate_series(@startDate::date, @endDate::date, interval '1 day')::date
                )
                select
                    DateList.""受注日"",
                    coalesce(sum(""受注"".""合計金額""), 0) as ""受注金額""
                from
                    DateList
                    left join ""受注""
                        on ""受注"".""受注日""::date = DateList.""受注日""
                group by
                    DateList.""受注日""
                order by
                    DateList.""受注日""
            ";
            var param = new
            {
                startDate,
                endDate
            };
            var list = await conn.QueryAsync<日別受注金額Model>(dataSql, param);

            return list;
        }

        public async Task<IEnumerable<日別受注金額Model>> Get週別受注金額Async(DateTime startDate, DateTime endDate)
        {
            using var conn = new NpgsqlConnection(_connectionString);

            // 1週間の開始は: 今日 - ((int)DateTime.Today.DayOfWeek + 1) % 7日
            // つまり「(今日の曜日番号+1)%7」日進めると週の始まり
            // 例: 今日が金曜(5) → 6日進めて土曜(6)

            var jst = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
            var basedDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, jst).Date;

            int todayWeekday = (int)basedDate.DayOfWeek; // 0=日曜, ... 6=土曜
            var weekStartOffset = (todayWeekday + 1) % 7;
            var firstWeekStart = startDate.AddDays(weekStartOffset);

            // 動的日付リスト生成＆ゼロ補完
            var dataSql = @"
                with WeekList(""受注日"") as (
                    select generate_series(@firstWeekStart::date, @endDate::date, interval '7 day')::date
                )
                select
                    WeekList.""受注日"",
                    coalesce(sum(""受注"".""合計金額""), 0) as ""受注金額""
                from
                    WeekList
                    left join ""受注""
                        on ""受注"".""受注日""::date >= WeekList.""受注日""
                       and ""受注"".""受注日""::date < WeekList.""受注日"" + interval '7 day'
                group by
                    WeekList.""受注日""
                order by
                    WeekList.""受注日""
            ";
            var param = new
            {
                firstWeekStart,
                endDate
            };
            var list = await conn.QueryAsync<日別受注金額Model>(dataSql, param);

            return list;
        }

        public async Task<IEnumerable<得意先別受注金額Model>> Get得意先別受注金額Async(DateTime startDate, DateTime endDate)
        {
            using var conn = new NpgsqlConnection(_connectionString);

            var dataSql = @"
                with Ranked as (
                    select
                        ""得意先ID"",
                        ""得意先名"",
                        sum(""合計金額"") as ""受注金額""
                    from ""受注""
                    where ""受注日"" between @startDate and @endDate
                    group by ""得意先ID"", ""得意先名""
                ),
                Top10 as (
                    select * from Ranked order by ""受注金額"" desc limit 10
                ),
                Other as (
                    select
                        -1 as ""得意先ID"",
                        'その他' as ""得意先名"",
                        coalesce(sum(""受注金額""),0) as ""受注金額""
                    from Ranked
                    where ""得意先ID"" not in (select ""得意先ID"" from Top10)
                )
                select * from Top10
                union all
                select * from Other
                order by ""受注金額"" desc
            ";
            var param = new
            {
                startDate,
                endDate
            };
            var list = await conn.QueryAsync<得意先別受注金額Model>(dataSql, param);

            return list;
        }

        public async Task<IEnumerable<商品別受注金額Model>> Get商品別受注金額Async(DateTime startDate, DateTime endDate)
        {
            using var conn = new NpgsqlConnection(_connectionString);

            var dataSql = @"
                with Ranked as (
                    select
                        m.""商品コード"",
                        m.""商品名"",
                        sum(m.""単価"" * m.""数量"") as ""受注金額""
                    from ""受注"" o
                        join ""受注明細"" m on o.""受注ID"" = m.""受注ID""
                    where o.""受注日"" between @startDate and @endDate
                    group by m.""商品コード"", m.""商品名""
                ),
                Top10 as (
                    select * from Ranked order by ""受注金額"" desc limit 10
                ),
                Other as (
                    select
                        'その他' as ""商品コード"",
                        '' as ""商品名"",
                        coalesce(sum(""受注金額""),0) as ""受注金額""
                    from Ranked
                    where ""商品コード"" not in (select ""商品コード"" from Top10)
                )
                select * from Top10
                union all
                select * from Other
                order by ""受注金額"" desc
            ";
            var param = new
            {
                startDate,
                endDate
            };
            var list = await conn.QueryAsync<商品別受注金額Model>(dataSql, param);

            return list;
        }
    }
}
