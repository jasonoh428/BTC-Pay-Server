using System.Collections.Generic;
using BTCPayServer.Data;

namespace BTCPayServer.U2F.Models
{
    public class U2FAuthenticationViewModel
    {
        public string StatusMessage { get; set; }
        public List<U2FDevice> Devices { get; set; }
    }
}
