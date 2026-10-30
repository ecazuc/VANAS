using Microsoft.EntityFrameworkCore;
using VANAS.Server.Data;

namespace VANAS.Server.Services
{
    public class BalanceService
    {
        private readonly AppDbContext _db;

        public BalanceService(AppDbContext db) => _db = db;

        /// <summary>Credits minus time consumed in sessions.</summary>
        public async Task<double> GetMinutesAsync(int userId)
        {
            var credits = await _db.TimeLedger.Where(t => t.UserId == userId).SumAsync(t => (double?)t.Minutes) ?? 0;
            var used = await _db.Sessions.Where(s => s.UserId == userId).SumAsync(s => (double?)s.MinutesConsumed) ?? 0;
            return Math.Max(0, credits - used);
        }
    }
}
