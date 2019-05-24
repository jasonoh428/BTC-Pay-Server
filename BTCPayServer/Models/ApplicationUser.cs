﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Authentication.OpenId.Models;
using Microsoft.AspNetCore.Identity;
using BTCPayServer.Data;
using BTCPayServer.Services.U2F.Models;
using BTCPayServer.Storage.Models;

namespace BTCPayServer.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public List<UserStore> UserStores
        {
            get;
            set;
        }

        public bool RequiresEmailConfirmation
        {
            get; set;
        }
        
        public List<BTCPayOpenIdClient> OpenIdClients { get; set; }
        
        public List<StoredFile> StoredFiles
        {
            get;
            set;
        }
        
        public List<U2FDevice> U2FDevices { get; set; }
    }
}
