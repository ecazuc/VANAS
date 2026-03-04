using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VANAS.Shared.Models
{
    public class GameItem
    {
        public string Title { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public string IconPath { get; set; } = string.Empty; // URL or local path
    }
}
