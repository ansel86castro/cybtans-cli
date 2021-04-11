using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cybtans.Proto.Generator.Licencing
{
    public class LicenseModel
    {
        public string Id { get; set; }

        public string CreateAt { get; set; }

        public int Duration { get; set; }

        public string Signature { get; set; }
    }
}
