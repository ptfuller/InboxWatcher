using System;
namespace InboxWatcher.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class NotificationAttribute : System.Attribute
    {
         public string NotificationName { get; }

        public NotificationAttribute(string name)
        {
            NotificationName = name;
        }
    }
}