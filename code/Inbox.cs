﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Media;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using notifier.Languages;
using notifier.Properties;

namespace notifier {
	class Inbox {

		// gmail api service
		private GmailService Api;

		// gmail address
		private string EmailAddress;

		// main inbox label
		private Google.Apis.Gmail.v1.Data.Label Box;

		// number of automatic reconnection
		private uint reconnect = 0;

		// unread threads
		private int? unreadthreads = 0;

		// last synchronization time
		private DateTime synctime = DateTime.Now;

		// reference to the main interface
		private Main Interface;

		/// <summary>
		/// Class constructor
		/// </summary>
		/// <param name="Credential">User credential given by the authorization broker</param>
		public Inbox(ref Main Form, ref UserCredential Credential) {
			Interface = Form;

			// initializes the gmail service base client api
			Api = new GmailService(new BaseClientService.Initializer() {
				HttpClientInitializer = Credential,
				ApplicationName = "Gmail notifier for Windows"
			});

			// retrieves the gmail address
			EmailAddress = Api.Users.GetProfile("me").Execute().EmailAddress;
		}

		/// <summary>
		/// Asynchronous method used to synchronize the user inbox
		/// </summary>
		/// <param name="timertick">Indicates if the synchronization come's from the timer tick or has been manually triggered</param>
		public async void Sync(bool timertick = false, bool token = false) {

			// prevents the application from syncing the inbox when updating
			if (Interface.UpdateService.IsUpdating()) {
				return;
			}

			// updates the synchronization time
			synctime = DateTime.Now;

			// resets reconnection count and prevents the application from displaying continuous warning icon when a timertick synchronization occurs after a reconnection attempt
			if (reconnect != 0) {
				timertick = false;
				reconnect = 0;
			}

			// disables the timeout when the user do a manual synchronization
			if (Interface.timer.Interval != Settings.Default.TimerInterval) {
				Timeout(Interface.menuItemTimeoutDisabled, Settings.Default.TimerInterval);

				// exits the method because the timeout function automatically restarts a synchronization once it has been disabled
				return;
			}

			// if internet is down, attempts to reconnect the user mailbox
			if (!Interface.ComputerService.IsInternetAvailable()) {
				Interface.timerReconnect.Enabled = true;
				Interface.timer.Enabled = false;

				return;
			}

			// refreshes the token if needed
			if (token) {
				await Interface.GmailService.RefreshToken();
			}

			// activates the necessary menu items
			Interface.menuItemSynchronize.Enabled = true;
			Interface.menuItemTimout.Enabled = true;
			Interface.menuItemSettings.Enabled = true;

			// displays the sync icon, but not on every tick of the timer
			if (!timertick) {
				Interface.notifyIcon.Icon = Resources.sync;
				Interface.notifyIcon.Text = translation.sync;
			}

			// do a small ping on the update service
			Interface.UpdateService.Ping();

			try {

				// manages the spam notification
				if (Settings.Default.SpamNotification) {

					// exits if a spam is already detected
					if (timertick && Interface.notifyIcon.Tag != null && Interface.notifyIcon.Tag.ToString() == "#spam") {
						return;
					}

					// gets the "spam" label
					Google.Apis.Gmail.v1.Data.Label spam = await Api.Users.Labels.Get("me", "SPAM").ExecuteAsync();

					// manages unread spams
					if (spam.ThreadsUnread > 0) {

						// sets the notification icon and text
						Interface.notifyIcon.Icon = Resources.spam;

						// plays a sound on unread spams
						if (Settings.Default.AudioNotification) {
							SystemSounds.Exclamation.Play();
						}

						// displays a balloon tip in the systray with the total of unread threads
						Interface.notifyIcon.ShowBalloonTip(450, spam.ThreadsUnread.ToString() + " " + (spam.ThreadsUnread > 1 ? translation.unreadSpams : translation.unreadSpam), translation.newUnreadSpam, ToolTipIcon.Error);
						Interface.notifyIcon.Text = spam.ThreadsUnread.ToString() + " " + (spam.ThreadsUnread > 1 ? translation.unreadSpams : translation.unreadSpam);
						Interface.notifyIcon.Tag = "#spam";

						return;
					}
				}

				// gets the "inbox" label
				Box = await Api.Users.Labels.Get("me", "INBOX").ExecuteAsync();

				// updates the statistics
				UpdateStatistics();

				// exits the sync if the number of unread threads is the same as before
				if (timertick && (Box.ThreadsUnread == unreadthreads)) {
					return;
				}

				// manages unread threads
				if (Box.ThreadsUnread > 0) {

					// sets the notification icon and text
					Interface.notifyIcon.Icon = Box.ThreadsUnread <= Settings.Default.UNSTACK_BOUNDARY ? Resources.mails : Resources.stack;

					// manages message notification
					if (Settings.Default.MessageNotification) {

						// plays a sound on unread threads
						if (Settings.Default.AudioNotification) {
							SystemSounds.Asterisk.Play();
						}

						// gets the message details
						UsersResource.MessagesResource.ListRequest messages = Api.Users.Messages.List("me");
						messages.LabelIds = "UNREAD";
						messages.MaxResults = 1;
						Google.Apis.Gmail.v1.Data.Message message = await Api.Users.Messages.Get("me", await messages.ExecuteAsync().ContinueWith(m => {
							return m.Result.Messages.First().Id;
						})).ExecuteAsync();

						//  displays a balloon tip in the systray with the total of unread threads and message details, depending on the user privacy setting
						if (Box.ThreadsUnread == 1 && Settings.Default.PrivacyNotification != (int)Notification.Privacy.All) {
							string subject = "";
							string from = "";

							foreach (MessagePartHeader header in message.Payload.Headers) {
								if (header.Name == "Subject") {
									subject = header.Value != "" ? header.Value : translation.newUnreadMessage;
								} else if (header.Name == "From") {
									Match match = Regex.Match(header.Value, ".* <");

									if (match.Length != 0) {
										from = match.Captures[0].Value.Replace(" <", "").Replace("\"", "");
									} else {
										match = Regex.Match(header.Value, "<?.*>?");
										from = match.Length != 0 ? match.Value.ToLower().Replace("<", "").Replace(">", "") : header.Value.Replace(match.Value, Box.ThreadsUnread.ToString() + " " + translation.unreadMessage);
									}
								}
							}

							if (Settings.Default.PrivacyNotification == (int)Notification.Privacy.None) {
								Interface.notifyIcon.ShowBalloonTip(450, from, message.Snippet != "" ? WebUtility.HtmlDecode(message.Snippet) : translation.newUnreadMessage, ToolTipIcon.Info);
							} else if (Settings.Default.PrivacyNotification == (int)Notification.Privacy.Short) {
								Interface.notifyIcon.ShowBalloonTip(450, from, subject, ToolTipIcon.Info);
							}
						} else {
							Interface.notifyIcon.ShowBalloonTip(450, Box.ThreadsUnread.ToString() + " " + (Box.ThreadsUnread > 1 ? translation.unreadMessages : translation.unreadMessage), translation.newUnreadMessage, ToolTipIcon.Info);
						}

						// manages the balloon tip click event handler: we store the "notification tag" to allow the user to directly display the specified view (inbox/message/spam) in a browser
						Interface.notifyIcon.Tag = "#inbox" + (Box.ThreadsUnread == 1 ? "/" + message.Id : "");
					}

					// displays the notification text
					Interface.notifyIcon.Text = Box.ThreadsUnread.ToString() + " " + (Box.ThreadsUnread > 1 ? translation.unreadMessages : translation.unreadMessage);

					// enables the mark as read menu item
					Interface.menuItemMarkAsRead.Text = translation.markAsRead + " (" + Box.ThreadsUnread + ")";
					Interface.menuItemMarkAsRead.Enabled = true;
				} else {

					// restores the default systray icon and text
					Interface.notifyIcon.Icon = Resources.normal;
					Interface.notifyIcon.Text = translation.noMessage;

					// disables the mark as read menu item
					Interface.menuItemMarkAsRead.Text = translation.markAsRead;
					Interface.menuItemMarkAsRead.Enabled = false;
				}

				// saves the number of unread threads
				unreadthreads = Box.ThreadsUnread;
			} catch (Exception exception) {

				// displays a balloon tip in the systray with the detailed error message
				Interface.notifyIcon.Icon = Resources.warning;
				Interface.notifyIcon.Text = translation.syncError;
				Interface.notifyIcon.ShowBalloonTip(1500, translation.error, translation.syncErrorOccured + exception.Message, ToolTipIcon.Warning);
			} finally {
				Interface.notifyIcon.Text = Interface.notifyIcon.Text.Split('\n')[0] + "\n" + translation.syncTime.Replace("{time}", synctime.ToLongTimeString());
			}
		}

		/// <summary>
		/// Asynchronous method used to mark as read the user inbox
		/// </summary>
		public async void MarkAsRead() {
			try {

				// updates the synchronization time
				synctime = DateTime.Now;

				// displays the sync icon
				Interface.notifyIcon.Icon = Resources.sync;
				Interface.notifyIcon.Text = translation.sync;

				// gets all unread threads
				UsersResource.ThreadsResource.ListRequest threads = Api.Users.Threads.List("me");
				threads.LabelIds = "UNREAD";
				ListThreadsResponse list = await threads.ExecuteAsync();
				IList<Google.Apis.Gmail.v1.Data.Thread> unread = list.Threads;

				// loops through all unread threads and removes the "unread" label for each one
				if (unread != null && unread.Count > 0) {
					foreach (Google.Apis.Gmail.v1.Data.Thread thread in unread) {
						ModifyThreadRequest request = new ModifyThreadRequest() {
							RemoveLabelIds = new List<string>() { "UNREAD" }
						};

						await Api.Users.Threads.Modify(request, "me", thread.Id).ExecuteAsync();
					}

					// gets the "inbox" label
					Box = await Api.Users.Labels.Get("me", "INBOX").ExecuteAsync();

					// updates the statistics
					UpdateStatistics();
				}

				// restores the default systray icon and text
				Interface.notifyIcon.Icon = Resources.normal;
				Interface.notifyIcon.Text = translation.noMessage;

				// restores the default tag
				Interface.notifyIcon.Tag = null;

				// disables the mark as read menu item
				Interface.menuItemMarkAsRead.Text = translation.markAsRead;
				Interface.menuItemMarkAsRead.Enabled = false;
			} catch (Exception exception) {

				// enabled the mark as read menu item
				Interface.menuItemMarkAsRead.Text = translation.markAsRead + " (" + Box.ThreadsUnread + ")";
				Interface.menuItemMarkAsRead.Enabled = true;

				// displays a balloon tip in the systray with the detailed error message
				Interface.notifyIcon.Icon = Resources.warning;
				Interface.notifyIcon.Text = translation.markAsReadError;
				Interface.notifyIcon.ShowBalloonTip(1500, translation.error, translation.markAsReadErrorOccured + exception.Message, ToolTipIcon.Warning);
			} finally {
				Interface.notifyIcon.Text = Interface.notifyIcon.Text.Split('\n')[0] + "\n" + translation.syncTime.Replace("{time}", synctime.ToLongTimeString());
			}
		}

		/// <summary>
		/// Delays the inbox sync during a certain time
		/// </summary>
		/// <param name="item">Item selected in the menu</param>
		/// <param name="delay">Delay until the next inbox sync, 0 means "infinite" timeout</param>
		public void Timeout(MenuItem item, int delay) {

			// exits if the selected menu item is already checked
			if (item.Checked) {
				return;
			}

			// unchecks all menu items
			foreach (MenuItem i in Interface.menuItemTimout.MenuItems) {
				i.Checked = false;
			}

			// displays the user choice
			item.Checked = true;

			// disables the timer if the delay is set to "infinite"
			Interface.timer.Enabled = delay != 0;

			// applies "1" if the delay is set to "infinite" because the timer delay attribute does not support "0"
			Interface.timer.Interval = delay != 0 ? delay : 1;

			// restores the default tag
			Interface.notifyIcon.Tag = null;

			// updates the systray icon and text
			if (delay != Settings.Default.TimerInterval) {
				Interface.notifyIcon.Icon = Resources.timeout;
				Interface.notifyIcon.Text = translation.timeout + " - " + (delay != 0 ? DateTime.Now.AddMilliseconds(delay).ToShortTimeString() : "∞");
			} else {
				Sync();
			}
		}

		//
		public void Retry() {
			// increases the number of reconnection attempt
			this.reconnect++;

			// bypass the first reconnection attempt because the last synchronization have already checked the internet connectivity
			if (this.reconnect == 1) {

				// sets the reconnection interval
				Interface.timerReconnect.Interval = Settings.Default.INTERVAL_RECONNECT * 1000;

				// disables the menu items
				Interface.menuItemSynchronize.Enabled = false;
				Interface.menuItemTimout.Enabled = false;
				Interface.menuItemSettings.Enabled = false;

				// displays the reconnection attempt message on the icon
				Interface.notifyIcon.Icon = Resources.retry;
				Interface.notifyIcon.Text = translation.reconnectAttempt;

				return;
			}

			// if internet is down, waits for INTERVAL_RECONNECT seconds before next attempt
			if (!Interface.ComputerService.IsInternetAvailable()) {

				// after max unsuccessull reconnection attempts, the application waits for the next sync
				if (this.reconnect == Settings.Default.MAX_AUTO_RECONNECT) {
					Interface.timerReconnect.Enabled = false;
					Interface.timerReconnect.Interval = 100;
					Interface.timer.Enabled = true;

					// activates the necessary menu items to allow the user to manually sync the inbox
					Interface.menuItemSynchronize.Enabled = true;

					// displays the last reconnection message on the icon
					Interface.notifyIcon.Icon = Resources.warning;
					Interface.notifyIcon.Text = translation.reconnectFailed;
				}
			} else {

				// restores default operation when internet is available
				Interface.timerReconnect.Enabled = false;
				Interface.timerReconnect.Interval = 100;
				Interface.timer.Enabled = true;

				// syncs the user mailbox
				Sync();
			}
		}

		/// <summary>
		/// Disposes the gmail api
		/// </summary>
		public void Dispose() {
			if (Api != null) {
				Api.Dispose();
			}
		}

		/// <summary>
		/// Returns the gmail email address
		/// </summary>
		public string GetEmailAddress() {
			return EmailAddress;
		}

		/// <summary>
		/// Returns the number of automatic reconnection to the network
		/// </summary>
		public uint GetReconnect() {
			return reconnect;
		}

		/// <summary>
		/// Sets the number of automatic reconnection to the network
		/// </summary>
		public void SetReconnect(uint reconnection) {
			reconnect = reconnection;
		}

		/// <summary>
		/// Returns the last synchronization time
		/// </summary>
		/// <returns></returns>
		public DateTime GetSyncTime() {
			return synctime;
		}
		
		/// <summary>
		/// Sets the synchronization time
		/// </summary>
		public void SetSyncTime(DateTime time) {
			synctime = time;
		}

		/// <summary>
		/// Asynchronous method used to get account statistics
		/// </summary>
		private async void UpdateStatistics() {
			try {
				Interface.labelTotalUnreadMails.Text = Box.ThreadsUnread.ToString();
				Interface.labelTotalMails.Text = Box.ThreadsTotal.ToString();

				ListDraftsResponse drafts = await Api.Users.Drafts.List("me").ExecuteAsync();
				ListLabelsResponse labels = await Api.Users.Labels.List("me").ExecuteAsync();

				if (drafts.Drafts != null) {
					Interface.labelTotalDrafts.Text = drafts.Drafts.Count.ToString();
				}

				if (labels.Labels != null) {
					Interface.labelTotalLabels.Text = labels.Labels.Count.ToString();
				}
			} catch (Exception) {
				// nothing to catch
			}
		}
	}
}
