using System.IO;
using System.Text;

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
    internal class Writer : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
