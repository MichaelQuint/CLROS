using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace OS.Tooling;

public sealed record ContractMismatch(string Contract, string Expected, string Actual);
