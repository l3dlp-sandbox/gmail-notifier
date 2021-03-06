using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows.Forms;
using notifier.Languages;
using notifier.Properties;

namespace notifier {
	public partial class Main : Form {

		/// <summary>
		/// Update service class
		/// </summary>
		internal Update UpdateService;

		/// <summary>
		/// Computer service class
		/// </summary>
		internal Computer ComputerService;

		/// <summary>
		/// Gmail service class
		/// </summary>
		internal Gmail GmailService;

		/// <summary>
		/// Gmail service class
		/// </summary>
		internal Notification NotificationService;

		/// <summary>
		/// Global UI tooltip
		/// </summary>
		internal ToolTip tip = new ToolTip();

		/// <summary>
		/// Initializes the class
		/// </summary>
		public Main() {
			InitializeComponent();

			// main application instance
			Main ui = this;

			// initializes services
			UpdateService = new Update(ref ui);
			ComputerService = new Computer(ref ui);
			GmailService = new Gmail(ref ui);
			NotificationService = new Notification(ref ui);
		}

		/// <summary>
		/// Loads the form
		/// </summary>
		private void Main_Load(object sender, EventArgs e) {

			// plays a pop sound at application startup
			if (Settings.Default.AudioPop) {
				using (SoundPlayer player = new SoundPlayer(Resources.pop_open)) {
					player.Play();
				}
			}

			// hides the form by default
			Visible = false;

			// upgrades the user configuration if necessary
			if (Settings.Default.UpdateRequired) {
				Settings.Default.Upgrade();

				// switches the update required state
				Settings.Default.UpdateRequired = false;
				Settings.Default.Save();

				// deletes the setup installer package from the previous upgrade
				UpdateService.DeleteSetupPackage();
			}

			// displays a systray notification on first load
			if (Settings.Default.FirstLoad && !Directory.Exists(Core.ApplicationDataFolder)) {
				NotificationService.Tip(Translation.welcome, Translation.firstLoad, Notification.Type.Info, 7000);

				// switches the first load state
				Settings.Default.FirstLoad = false;
				Settings.Default.Save();

				// waits for 7 seconds to complete the thread
				System.Threading.Thread.Sleep(7000);
			}

			// configures the help provider
			HelpProvider help = new HelpProvider();
			help.SetHelpString(fieldRunAtWindowsStartup, Translation.helpRunAtWindowsStartup);
			help.SetHelpString(fieldAskonExit, Translation.helpAskonExit);
			help.SetHelpString(fieldLanguage, Translation.helpLanguage);
			help.SetHelpString(labelEmailAddress, Translation.helpEmailAddress);
			help.SetHelpString(labelTokenDelivery, Translation.helpTokenDelivery);
			help.SetHelpString(buttonGmailDisconnect, Translation.helpGmailDisconnect);
			help.SetHelpString(chartUnreadMails, Translation.helpStatistics);
			help.SetHelpString(chartTotalMails, Translation.helpStatistics);
			help.SetHelpString(chartInbox, Translation.helpStatistics);
			help.SetHelpString(labelTotalDrafts, Translation.helpStatistics);
			help.SetHelpString(labelTotalLabels, Translation.helpStatistics);
			help.SetHelpString(fieldMessageNotification, Translation.helpMessageNotification);
			help.SetHelpString(fieldAudioNotification, Translation.helpAudioNotification);
			help.SetHelpString(fieldSpamNotification, Translation.helpSpamNotification);
			help.SetHelpString(fieldNumericDelay, Translation.helpNumericDelay);
			help.SetHelpString(fieldStepDelay, Translation.helpStepDelay);
			help.SetHelpString(fieldNotificationBehavior, Translation.helpNotificationBehavior);
			help.SetHelpString(fieldPrivacyNotificationNone, Translation.helpPrivacyNotificationNone);
			help.SetHelpString(fieldPrivacyNotificationShort, Translation.helpPrivacyNotificationShort);
			help.SetHelpString(fieldPrivacyNotificationAll, Translation.helpPrivacyNotificationAll);
			help.SetHelpString(fieldUpdateService, Translation.helpUpdateService);
			help.SetHelpString(fieldUpdateDownload, Translation.helpUpdateDownload);
			help.SetHelpString(fieldUpdateQuiet, Translation.helpUpdateQuiet);
			help.SetHelpString(fieldUpdatePeriod, Translation.helpUpdatePeriod);
			help.SetHelpString(labelUpdateControl, Translation.helpUpdateControl);
			help.SetHelpString(buttonCheckForUpdate, Translation.helpCheckForUpdate);

			// authenticates the user
			GmailService.Authentication();

			// attaches the context menu to the systray icon
			notifyIcon.ContextMenu = notifyMenu;

			// binds the "PropertyChanged" event of the settings to automatically save the user settings and display the setting label
			Settings.Default.PropertyChanged += new PropertyChangedEventHandler((object source, PropertyChangedEventArgs target) => {
				Settings.Default.Save();

				if (target.PropertyName != "UpdateControl") {
					labelSettingsSaved.Visible = true;
				}
			});

			// binds all computer services
			ComputerService.BindNetwork();
			ComputerService.BindPowerMode();
			ComputerService.BindSessionSwitch();

			// displays the open message label
			labelNotificationOpenMessage.Visible = Settings.Default.NotificationBehavior == 1;

			// displays the step delay setting
			fieldStepDelay.SelectedIndex = (int)Settings.Default.StepDelay;

			// displays the notification behavior setting
			fieldNotificationBehavior.SelectedIndex = (int)Settings.Default.NotificationBehavior;

			// displays the privacy notification setting
			switch (Settings.Default.PrivacyNotification) {
				case (int)Notification.Privacy.None:
					fieldPrivacyNotificationNone.Checked = true;
					pictureBoxPrivacyPreview.Image = Resources.privacy_none;
					break;
				default:
				case (int)Notification.Privacy.Short:
					fieldPrivacyNotificationShort.Checked = true;
					pictureBoxPrivacyPreview.Image = Resources.privacy_short;
					break;
				case (int)Notification.Privacy.All:
					fieldPrivacyNotificationAll.Checked = true;
					pictureBoxPrivacyPreview.Image = Resources.privacy_all;
					break;
			}

			// displays the update period setting
			fieldUpdatePeriod.SelectedIndex = (int)Settings.Default.UpdatePeriod;

			// displays the update control setting
			labelUpdateControl.Text = Settings.Default.UpdateControl.ToString();

			// displays the product version
			linkVersion.Text = Core.Version.Substring(1);

			// displays a tooltip for the product version
			tip.SetToolTip(linkVersion, Settings.Default.GITHUB_REPOSITORY + "/releases/tag/" + Core.Version);

			// displays a tooltip for the license link
			tip.SetToolTip(linkPrivacy, Settings.Default.GITHUB_REPOSITORY + "/blob/master/PRIVACY.md");

			// displays a tooltip for the website link
			tip.SetToolTip(linkWebsiteYusuke, Settings.Default.SITE_YUSUKE);

			// displays a tooltip for the website link
			tip.SetToolTip(linkWebsiteXavier, Settings.Default.SITE_AUTHOR);

			// displays a tooltip for the license link
			tip.SetToolTip(linkLicense, Settings.Default.GITHUB_REPOSITORY + "/blob/master/LICENSE.md");
		}

		/// <summary>
		/// Prompts the user before closing the form
		/// </summary>
		private void Main_FormClosing(object sender, FormClosingEventArgs e) {

			// hides the form to the systray if closed by the user
			if (e.CloseReason == CloseReason.UserClosing) {
				labelSettingsSaved.Visible = false;
				WindowState = FormWindowState.Minimized;
				ShowInTaskbar = false;
				Visible = false;
				e.Cancel = true;

				return;
			}

			// disposes the gmail service
			GmailService.Dispose();

			// plays a pop sound at application exit
			if (Settings.Default.AudioPop) {
				using (SoundPlayer player = new SoundPlayer(Resources.pop_exit)) {
					player.PlaySync();
				}
			}
		}

		/// <summary>
		/// Manages the RunAtWindowsStartup user setting
		/// </summary>
		private void FieldRunAtWindowsStartup_CheckedChanged(object sender, EventArgs e) {
			ComputerService.SetApplicationStartup(fieldRunAtWindowsStartup.Checked ? Computer.Registration.On : Computer.Registration.Off);
		}

		/// <summary>
		/// Manages the Language user setting
		/// </summary>
		private void FieldLanguage_SelectionChangeCommitted(object sender, EventArgs e) {

			// discard changes if the user select the current application language
			if (fieldLanguage.Text == Settings.Default.Language) {
				return;
			}

			// sets the new application language
			Settings.Default.Language = fieldLanguage.Text;

			// gets the current systemthreading culture
			string culture = System.Threading.Thread.CurrentThread.CurrentUICulture.Name;

			// indicates to the user that to apply the new language on the interface, the application must be restarted
			bool changes = !((culture == "en-US" && fieldLanguage.Text == "English") || (culture == "fr-FR" && fieldLanguage.Text == "Français") || (culture == "de-DE" && fieldLanguage.Text == "Deutsch"));

			labelRestartToApply.Visible = changes;
			linkRestartToApply.Visible = changes;
		}

		/// <summary>
		/// Manages the SpamNotification user setting
		/// </summary>
		private void FieldSpamNotification_Click(object sender, EventArgs e) {
			GmailService.Inbox.Sync();
		}

		/// <summary>
		/// Manages the NumericDelay user setting
		/// </summary>
		private void FieldNumericDelay_ValueChanged(object sender, EventArgs e) {
			Settings.Default.TimerInterval = 1000 * (fieldStepDelay.SelectedIndex == 0 ? 60 : 3600) * (int)fieldNumericDelay.Value;
			Settings.Default.NumericDelay = fieldNumericDelay.Value;
			timer.Interval = (int)Settings.Default.TimerInterval;
		}

		/// <summary>
		/// Manages the StepDelay user setting
		/// </summary>
		private void FieldStepDelay_SelectionChangeCommitted(object sender, EventArgs e) {
			Settings.Default.TimerInterval = 1000 * (fieldStepDelay.SelectedIndex == 0 ? 60 : 3600) * (int)fieldNumericDelay.Value;
			Settings.Default.StepDelay = (uint)fieldStepDelay.SelectedIndex;
			timer.Interval = (int)Settings.Default.TimerInterval;
		}

		/// <summary>
		/// Manages the NotificationBehavior user setting
		/// </summary>
		private void FieldNotificationBehavior_SelectionChangeCommitted(object sender, EventArgs e) {
			Settings.Default.NotificationBehavior = (uint)fieldNotificationBehavior.SelectedIndex;
			labelNotificationOpenMessage.Visible = Settings.Default.NotificationBehavior == 1;
		}

		/// <summary>
		/// Manages the PrivacyNotificationNone user setting
		/// </summary>
		private void FieldPrivacyNotificationNone_CheckedChanged(object sender, EventArgs e) {
			Settings.Default.PrivacyNotification = (int)Notification.Privacy.None;
			pictureBoxPrivacyPreview.Image = Resources.privacy_none;
		}

		/// <summary>
		/// Manages the PrivacyNotificationShort user setting
		/// </summary>
		private void FieldPrivacyNotificationShort_CheckedChanged(object sender, EventArgs e) {
			Settings.Default.PrivacyNotification = (int)Notification.Privacy.Short;
			pictureBoxPrivacyPreview.Image = Resources.privacy_short;
		}

		/// <summary>
		/// Manages the PrivacyNotificationAll user setting
		/// </summary>
		private void FieldPrivacyNotificationAll_CheckedChanged(object sender, EventArgs e) {
			Settings.Default.PrivacyNotification = (int)Notification.Privacy.All;
			pictureBoxPrivacyPreview.Image = Resources.privacy_all;
		}

		/// <summary>
		/// Manages the UpdatePeriod user setting
		/// </summary>
		private void FieldUpdatePeriod_SelectedIndexChanged(object sender, EventArgs e) {
			Settings.Default.UpdatePeriod = (uint)fieldUpdatePeriod.SelectedIndex;
		}

		/// <summary>
		/// Opens the Github release section of the current build
		/// </summary>
		private void LinkVersion_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
			Process.Start(Settings.Default.GITHUB_REPOSITORY + "/releases/tag/" + Core.Version);
		}

		/// <summary>
		/// Opens the Github privacy notice file
		/// </summary>
		private void linkPrivacy_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
			Process.Start(Settings.Default.GITHUB_REPOSITORY + "/blob/master/PRIVACY.md");
		}

		/// <summary>
		/// Opens the Yusuke website
		/// </summary>
		private void LinkWebsiteYusuke_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
			Process.Start(Settings.Default.SITE_YUSUKE);
		}

		/// <summary>
		/// Opens the Xavier website
		/// </summary>
		private void LinkWebsiteXavier_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
			Process.Start(Settings.Default.SITE_AUTHOR);
		}

		/// <summary>
		/// Opens the Github license file
		/// </summary>
		private void LinkLicense_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
			Process.Start(Settings.Default.GITHUB_REPOSITORY + "/blob/master/LICENSE.md");
		}

		/// <summary>
		/// Hides the settings saved label
		/// </summary>
		private void TabControl_Selecting(object sender, TabControlCancelEventArgs e) {
			labelSettingsSaved.Visible = false;
		}

		/// <summary>
		/// Closes the preferences when the OK button is clicked
		/// </summary>
		private void ButtonOK_Click(object sender, EventArgs e) {
			labelSettingsSaved.Visible = false;
			WindowState = FormWindowState.Minimized;
			ShowInTaskbar = false;
			Visible = false;
		}

		/// <summary>
		/// Closes the preferences when the Escape key is pressed
		/// </summary>
		private void Main_KeyUp(object sender, KeyEventArgs e) {
			if (e.KeyCode != Keys.Escape) {
				return;
			}

			labelSettingsSaved.Visible = false;
			WindowState = FormWindowState.Minimized;
			ShowInTaskbar = false;
			Visible = false;
		}

		/// <summary>
		/// Manages the context menu New message item
		/// </summary>
		private void MenuItemNewMessage_Click(object sender, EventArgs e) {
			Process.Start(Settings.Default.GMAIL_BASEURL + "/#inbox?compose=new");
		}

		/// <summary>
		/// Manages the context menu Synchronize item
		/// </summary>
		private void MenuItemSynchronize_Click(object sender, EventArgs e) {
			GmailService.Inbox.Sync();
		}

		/// <summary>
		/// Manages the context menu MarkAsRead item
		/// </summary>
		private void MenuItemMarkAsRead_Click(object sender, EventArgs e) {
			GmailService.Inbox.MarkAsRead();
		}

		/// <summary>
		/// Manages the context menu TimeoutDisabled item
		/// </summary>
		private void MenuItemTimeoutDisabled_Click(object sender, EventArgs e) {
			NotificationService.Resume();
		}

		/// <summary>
		/// Manages the context menu Timeout30m item
		/// </summary>
		private void MenuItemTimeout30m_Click(object sender, EventArgs e) {
			NotificationService.Pause((MenuItem)sender, 1000 * 60 * 30);
		}

		/// <summary>
		/// Manages the context menu Timeout1h item
		/// </summary>
		private void MenuItemTimeout1h_Click(object sender, EventArgs e) {
			NotificationService.Pause((MenuItem)sender, 1000 * 60 * 60);
		}

		/// <summary>
		/// Manages the context menu Timeout2h item
		/// </summary>
		private void MenuItemTimeout2h_Click(object sender, EventArgs e) {
			NotificationService.Pause((MenuItem)sender, 1000 * 60 * 60 * 2);
		}

		/// <summary>
		/// Manages the context menu Timeout5h item
		/// </summary>
		private void MenuItemTimeout5h_Click(object sender, EventArgs e) {
			NotificationService.Pause((MenuItem)sender, 1000 * 60 * 60 * 5);
		}

		/// <summary>
		/// Manages the context menu TimeoutIndefinitely item
		/// </summary>
		private void MenuItemTimeoutIndefinitely_Click(object sender, EventArgs e) {
			NotificationService.Pause((MenuItem)sender, 0);
		}

		/// <summary>
		/// Manages the context menu Settings item
		/// </summary>
		private void MenuItemSettings_Click(object sender, EventArgs e) {

			// resets the settings label visibility
			labelSettingsSaved.Visible = false;

			// checks the start with Windows setting against the registry
			if (tabControl.SelectedTab == tabPageGeneral) {
				ComputerService.RegulatesRegistry();
			}

			// displays the form
			Visible = true;
			ShowInTaskbar = true;
			WindowState = FormWindowState.Normal;
			Focus();
		}

		/// <summary>
		/// Manages the context menu exit item
		/// </summary>
		private void MenuItemExit_Click(object sender, EventArgs e) {

			// asks the user for exit, depending on the application settings
			if (Settings.Default.AskonExit) {
				DialogResult dialog = MessageBox.Show(Translation.applicationExitQuestion, Translation.applicationExit, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

				if (dialog == DialogResult.No) {
					return;
				}
			}

			Application.Exit();
		}

		/// <summary>
		/// Manages the systray icon double click
		/// </summary>
		private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e) {
			if (e.Button == MouseButtons.Left) {
				NotificationService.Interaction();
			}
		}

		/// <summary>
		/// Manages the systray icon balloon click
		/// </summary>
		private void NotifyIcon_BalloonTipClicked(object sender, EventArgs e) {
			if ((Control.MouseButtons & MouseButtons.Right) == MouseButtons.Right) {
				return;
			}

			NotificationService.Interaction(true);
		}

		/// <summary>
		/// Synchronizes the user mailbox on every timer tick
		/// </summary>
		private void Timer_Tick(object sender, EventArgs e) {

			// restores the timer interval when the timeout time has elapsed
			if (NotificationService.Paused) {
				NotificationService.Resume();

				return;
			}

			// synchronizes the inbox
			GmailService.Inbox.Sync(false);
		}

		/// <summary>
		/// Disconnects the Gmail account from the application
		/// </summary>
		private void ButtonGmailDisconnect_Click(object sender, EventArgs e) {

			// asks the user for disconnect
			DialogResult dialog = MessageBox.Show(Translation.gmailDisconnectQuestion.Replace("{account_name}", labelEmailAddress.Text), Translation.gmailDisconnect, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

			if (dialog == DialogResult.No) {
				return;
			}

			// deletes the local application data folder and the client token file
			if (Directory.Exists(Core.ApplicationDataFolder)) {
				Directory.Delete(Core.ApplicationDataFolder, true);
			}

			// restarts the application
			Core.RestartApplication();
		}

		/// <summary>
		/// Restarts the application to apply new user settings
		/// </summary>
		private void LinkRestartToApply_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
			Core.RestartApplication();
		}

		// attempts to reconnect the user mailbox
		private void TimerReconnect_Tick(object sender, EventArgs e) {
			GmailService.Inbox.Retry();
		}

		/// <summary>
		/// Check for update
		/// </summary>
		private void ButtonCheckForUpdate_Click(object sender, EventArgs e) {
			buttonCheckForUpdate.Enabled = false;

			if (UpdateService.UpdateAvailable) {
				WindowState = FormWindowState.Minimized;
				ShowInTaskbar = false;
				Visible = false;
				UpdateService.Download();
			} else {
				UpdateService.Check();
			}
		}

		/// <summary>
		/// Checks the start with Windows setting against the registry when entering the general tab page
		/// </summary>
		private void tabPageGeneral_Enter(object sender, EventArgs e) {
			ComputerService.RegulatesRegistry();
		}
	}
}