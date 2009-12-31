﻿using System;
using System.Windows.Forms;
using Palaso.WritingSystems;

namespace Palaso.UI.WindowsForms.WritingSystems.WSIdentifiers
{
	public partial class IpaIdentifierView : UserControl
	{
		private readonly WritingSystemSetupPM _model;
		private bool _updatingFromModel;

		public IpaIdentifierView(WritingSystemSetupPM model)
		{
			_model = model;
			InitializeComponent();
			if (model != null)
			{
				model.SelectionChanged += UpdateDisplayFromModel;
			}
			UpdateDisplayFromModel(null,null);
		}

		private void UpdateDisplayFromModel(object sender, EventArgs e)
		{
			if (_model.CurrentDefinition != null)
			{
				_updatingFromModel = true;
				//minus one because we skip the "not ipa" choice
				comboBox1.SelectedItem = _model.CurrentIpaStatus-1;
				_updatingFromModel = false;
			}
		}

		private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			System.Diagnostics.Process.Start("http://en.wikipedia.org/wiki/International_Phonetic_Alphabet");
		}

		public string ChoiceName
		{
			get { return "IPA Transcription"; }
		}

		private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			_model.CurrentIpaStatus =1+(IpaStatusChoices) comboBox1.SelectedIndex;
		}
	}
}