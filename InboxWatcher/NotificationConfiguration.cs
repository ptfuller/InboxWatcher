//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace InboxWatcher
{
    using System;
    using System.Collections.Generic;
    
    public partial class NotificationConfiguration
    {
        public int Id { get; set; }
        public string ConfigurationXml { get; set; }
        public int ImapMailBoxConfigurationId { get; set; }
        public string NotificationType { get; set; }
    
        public virtual ImapMailBoxConfiguration ImapMailBoxConfiguration { get; set; }
    }
}