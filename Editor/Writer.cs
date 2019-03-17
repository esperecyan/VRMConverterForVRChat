using System.IO;
using System.Text;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    internal class Writer : StringWriter
    {
        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }
}
