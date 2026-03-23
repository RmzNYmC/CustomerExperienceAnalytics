using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEA.Core.Enum
{
    public enum ComplaintStatus
    {
        New = 1,             // Yeni
        InProgress = 2,      // İşlemde
        WaitingForInfo = 3,  // Bilgi Bekleniyor
        Resolved = 4,        // Çözüldü
        Closed = 5,          // Kapatıldı
        Escalated = 6        // Üst Yönetime Aktarıldı
    }
}
