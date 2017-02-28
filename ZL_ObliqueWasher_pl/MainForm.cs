using System;

namespace ZL_ObliqueWasher_pl
{
    public partial class MainForm : Tekla.Structures.Dialog.PluginFormBase
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void OkApplyModifyGetOnOffCancel_OkClicked(object sender, EventArgs e)
        {
            this.Apply();
            this.Close();
        }

        private void OkApplyModifyGetOnOffCancel_ApplyClicked(object sender, EventArgs e)
        {
            this.Apply();
        }

        private void OkApplyModifyGetOnOffCancel_ModifyClicked(object sender, EventArgs e)
        {
            this.Modify();
        }

        private void OkApplyModifyGetOnOffCancel_GetClicked(object sender, EventArgs e)
        {
            this.Get();
        }

        private void OkApplyModifyGetOnOffCancel_OnOffClicked(object sender, EventArgs e)
        {
            this.ToggleSelection();
        }

        private void OkApplyModifyGetOnOffCancel_CancelClicked(object sender, EventArgs e)
        {
            this.Close();
        }

        private void materialCatalog1_Load(object sender, EventArgs e)
        {

        }

        private void materialCatalog1_SelectionDone(object sender, EventArgs e)
        {
            this.textBox5.Text = materialCatalog1.SelectedMaterial;
            SetAttributeValue(this.textBox5, materialCatalog1.SelectedMaterial);
        }

        private void materialCatalog1_SelectClicked(object sender, EventArgs e)
        {
            materialCatalog1.SelectedMaterial = this.textBox5.Text;
        }
    }
}