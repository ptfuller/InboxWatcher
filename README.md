# InboxWatcher
A Windows Service that monitors Imap mailboxes and provides a REST API to interact with messages.  Provides notification of messages received to configurable endpoint.  Work in progress.

InboxWatcher provides an email management solution geared towards a ticketing/helpdesk type environment where many people need to interact with emails that come in to one shared account.

todo: 
- [X] SignalR
- [X] Better handling of Smtp / sending emails
- [ ] Better README.md
- [ ] Add email filtering
- [ ] Improved status display on dashboard (show when starting up and when started via signalR)
- [ ] Charts on dashboard
- [ ] Search for old emails in mailbox
- [ ] Index all existing emails?
- [ ] Serialize emails to db as they come in for speed improvement / backup?
- [ ] Ability to choose DB type / hookup to existing db (don't force user to localdb)
- [ ] Factory for Notifications
- [ ] Additional notification types (sms, signalr, wcf, more?)
- [ ] Catch up on tests
