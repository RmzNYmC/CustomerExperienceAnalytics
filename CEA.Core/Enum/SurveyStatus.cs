using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEA.Core.Enum
{
    public enum SurveyStatus
    {
        Draft = 1,           // Taslak
        Active = 2,          // Aktif/Published
        Paused = 3,          // Durduruldu
        Completed = 4,       // Tamamlandı
        Archived = 5         // Arşivlendi
    }
}
