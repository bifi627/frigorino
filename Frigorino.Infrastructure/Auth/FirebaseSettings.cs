using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Frigorino.Infrastructure.Auth
{
    public class FirebaseSettings
    {
        public const string SECTION_NAME = "FirebaseSettings";
        public string ValidIssuer { get; set; } = "";
        public string ValidAudience { get; set; } = "";
        public string AccessJson { get; set; } = "";
    }
}
