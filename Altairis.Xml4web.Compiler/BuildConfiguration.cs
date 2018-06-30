﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Altairis.Xml4web.Compiler {
    public class BuildConfiguration {

        public static BuildConfiguration Load(string fileName) {
            Contract.Ensures(Contract.Result<BuildConfiguration>() != null);
            Contract.Requires(string.IsNullOrEmpty(fileName) == false);

            var json = File.ReadAllText(fileName);
            return JsonConvert.DeserializeObject<BuildConfiguration>(json);
        }

        public string FolderName { get; set; }

        public Dictionary<string, string> Transforms { get; set; }

    }
}
