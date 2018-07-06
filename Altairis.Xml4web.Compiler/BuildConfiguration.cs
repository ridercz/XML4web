using System;
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
            var obj = JsonConvert.DeserializeObject<BuildConfiguration>(json);
            obj.FileName = fileName;
            obj.ExpandAllPaths();
            return obj;
        }

        private string ExpandPath(string s) {
            if (Path.IsPathFullyQualified(s)) return s;
            return Path.Combine(Path.GetDirectoryName(this.FileName), s.Trim('/', '\\'));
        }

        private void ExpandAllPaths() {
            this.SourceFolder = this.ExpandPath(this.SourceFolder);
            this.TargetFolder = this.ExpandPath(this.TargetFolder);
            this.StaticFolder = this.ExpandPath(this.StaticFolder);
            this.XsltFolder = this.ExpandPath(this.XsltFolder);
            this.WorkFolder = this.ExpandPath(this.WorkFolder);
        }


        private string FileName { get; set; }

        public string SourceFolder { get; set; }

        public string TargetFolder { get; set; }

        public string StaticFolder { get; set; }

        public string XsltFolder { get; set; }

        public string WorkFolder { get; set; }

        public string PrependHtmlDoctype { get; set; }

        public Dictionary<string, string> Transforms { get; set; } = new Dictionary<string, string>();

        public Dictionary<string, string> RawTransforms { get; set; } = new Dictionary<string, string>();

        public Dictionary<string, string> TransformParameters { get; set; } = new Dictionary<string, string>();

    }
}
