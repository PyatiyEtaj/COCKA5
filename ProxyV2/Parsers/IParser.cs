using System.Text;

namespace ProxyV2.Parsers
{
    public interface IParser
    {
        object Parse(string str);
    }
}
