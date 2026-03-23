using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEA.Core.Enums
{
    public enum QuestionType
    {
        SingleChoice = 1,      // Tek seçim (Radio)
        MultipleChoice = 2,      // Çoklu seçim (Checkbox)
        RatingScale = 3,         // Derecelendirme (1-5, 1-10)
        TextOpen = 4,            // Açık uçlu metin
        NpsScore = 5             // Net Promoter Score (0-10)
    }
}
