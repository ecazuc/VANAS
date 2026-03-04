using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VANAS.Shared.Models
{
    public class User
    {
        public int Id { get; set; } // Primary Key
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty; // Never store plain passwords!
        public double BalanceMinutes { get; set; } // How much time they have left
        public bool IsAdmin { get; set; } // To distinguish between gamers and staff
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
