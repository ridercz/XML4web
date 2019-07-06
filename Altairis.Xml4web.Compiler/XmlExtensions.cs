using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Altairis.Xml4web.Compiler {
    public static class XmlExtensions {

        public static void AppendChildren(this XmlNode parent, IEnumerable<XmlNode> children) {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (!children.Any()) return;

            foreach (var item in children) {
                parent.AppendChild(item);
            }
        }

    }
}
