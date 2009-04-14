using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Palaso.WritingSystems;

namespace Palaso.UI.WindowsForms.WritingSystems
{
	public partial class WSPropertiesDialog : Form
	{
		private WritingSystemSetupPM _model;

		public WSPropertiesDialog()
		{
			InitializeComponent();
			_model = new WritingSystemSetupPM(new LdmlInFolderWritingSystemStore());
			_wsPropertiesPanel.BindToModel(_model);
		}

		public WSPropertiesDialog(string writingSystemStorePath)
		{
			InitializeComponent();
			_model = new WritingSystemSetupPM(new LdmlInFolderWritingSystemStore(writingSystemStorePath));
			_wsPropertiesPanel.BindToModel(_model);
		}

		public DialogResult  ShowDialog(string initiallySelectWritingSystemRfc4646)
		{
			_model.SetCurrentIndexFromRfc46464(initiallySelectWritingSystemRfc4646);
			return this.ShowDialog();
		}

		private void _closeButton_Click(object sender, EventArgs e)
		{
			Close();
		}

		protected override void OnClosed(EventArgs e)
		{
			_model.Save();
			base.OnClosed(e);
		}
	}
}