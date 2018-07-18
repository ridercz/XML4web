using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altairis.Xml4web.AzureSync {

    public enum JobOperationType {
        Undefined = 0,
        Ignore = 1,
        Upload = 2,
        Update = 3,
        Delete = 4,
    }

    public class JobOperation {

        public JobOperationType OperationType { get; set; }

        public string LogicalName { get; set; }

        public string LocalFileName { get; set; }

        public Uri StorageUri { get; set; }

        public string ContentHash { get; set; }

        public long Size { get; set; }

    }
}
