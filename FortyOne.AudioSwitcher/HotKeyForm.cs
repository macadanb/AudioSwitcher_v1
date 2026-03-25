using System;
using System.Windows.Forms;
using AudioSwitcher.AudioApi;
using FortyOne.AudioSwitcher.HotKeyData;

namespace FortyOne.AudioSwitcher
{
    public enum HotKeyFormMode
    {
        Normal,
        Edit
    }

    public partial class HotKeyForm : Form
    {
        private readonly HotKey _hotkey;
        private readonly HotKey _linkedHotKey;
        private readonly HotKeyFormMode _mode = HotKeyFormMode.Normal;
        private DeviceState _deviceStateFilter = DeviceState.Active;

        private bool _firstFocus = true;

        public HotKeyForm()
        {
            InitializeComponent();

            _hotkey = new HotKey();

            // Configuración de filtros de dispositivos según los ajustes del programa
            if (Program.Settings.ShowDisabledDevices)
                _deviceStateFilter |= DeviceState.Disabled;

            if (Program.Settings.ShowDisconnectedDevices)
                _deviceStateFilter |= DeviceState.Unplugged;

            // Limpiar y cargar ambos ComboBoxes (Normal y Long Press)
            cmbDevices.Items.Clear();
            cmbLongDevices.Items.Clear();

            // Añadir opción "Ninguno" para el Long Press
            cmbLongDevices.Items.Add(new { FullName = "(Ninguno)", Id = Guid.Empty });

            var playbackDevices = AudioDeviceManager.Controller.GetPlaybackDevices(_deviceStateFilter);
            var captureDevices = AudioDeviceManager.Controller.GetCaptureDevices(_deviceStateFilter);

            foreach (var ad in playbackDevices)
            {
                cmbDevices.Items.Add(ad);
                cmbLongDevices.Items.Add(ad);
            }

            foreach (var ad in captureDevices)
            {
                cmbDevices.Items.Add(ad);
                cmbLongDevices.Items.Add(ad);
            }

            cmbDevices.DisplayMember = "FullName";
            cmbDevices.ValueMember = "Id";

            cmbLongDevices.DisplayMember = "FullName";
            cmbLongDevices.ValueMember = "Id";
        }

        public HotKeyForm(HotKey hk)
            : this()
        {
            _linkedHotKey = hk;

            // Clonar los datos del HotKey existente al temporal
            _hotkey.DeviceId = hk.DeviceId;
            _hotkey.LongPressDeviceId = hk.LongPressDeviceId;
            _hotkey.LongPressDelay = hk.LongPressDelay;
            _hotkey.Key = hk.Key;
            _hotkey.Modifiers = hk.Modifiers;

            txtHotKey.Text = hk.HotKeyString;
            numDelay.Value = hk.LongPressDelay;
            _firstFocus = false;

            _mode = HotKeyFormMode.Edit;

            Text = "Edit Hot Key";
            btnAdd.Text = "Save";
        }

        private void HotKeyForm_Load(object sender, EventArgs e)
        {
            AudioSwitcher.Instance.DisableHotKeyFunction = true;

            // Seleccionar el dispositivo normal en el combo
            foreach (var o in cmbDevices.Items)
            {
                if (o is IDevice device && device.Id == _hotkey.DeviceId)
                {
                    cmbDevices.SelectedIndex = cmbDevices.Items.IndexOf(o);
                    break;
                }
            }

            // Seleccionar el dispositivo de pulsación larga
            if (_hotkey.LongPressDeviceId == Guid.Empty)
            {
                cmbLongDevices.SelectedIndex = 0; // "(Ninguno)"
            }
            else
            {
                foreach (var o in cmbLongDevices.Items)
                {
                    if (o is IDevice device && device.Id == _hotkey.LongPressDeviceId)
                    {
                        cmbLongDevices.SelectedIndex = cmbLongDevices.Items.IndexOf(o);
                        break;
                    }
                }
            }
        }

        private void txtHotKey_Enter(object sender, EventArgs e)
        {
            if (_firstFocus)
            {
                txtHotKey.Text = "";
                _firstFocus = false;
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (_mode == HotKeyFormMode.Normal && HotKeyManager.DuplicateHotKey(_hotkey))
                return;

            // Actualizar el delay desde el control numérico antes de guardar
            _hotkey.LongPressDelay = (int)numDelay.Value;

            if (_mode == HotKeyFormMode.Edit)
                HotKeyManager.DeleteHotKey(_linkedHotKey);

            // Registrar el nuevo HotKey con todos los datos (Normal + Long)
            if (HotKeyManager.AddHotKey(_hotkey))
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                errorProvider1.SetError(txtHotKey, "Hot Key is already registered");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void txtHotKey_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Menu)
                return;

            _hotkey.Key = e.KeyCode;
            _hotkey.Modifiers = Modifiers.None;

            if (e.Control)
                _hotkey.Modifiers |= Modifiers.Control;

            if (e.Alt)
                _hotkey.Modifiers |= Modifiers.Alt;

            if (e.Shift)
                _hotkey.Modifiers |= Modifiers.Shift;

            // Corregido: verificación de Win Key
            if (e.Modifiers == Keys.LWin || e.Modifiers == Keys.RWin)
                _hotkey.Modifiers |= Modifiers.Win;

            txtHotKey.Text = _hotkey.HotKeyString;

            if (_mode != HotKeyFormMode.Edit && HotKeyManager.DuplicateHotKey(_hotkey))
                errorProvider1.SetError(txtHotKey, "Duplicate Hot Key Detected");
        }

        private void cmbDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbDevices.SelectedItem is IDevice device)
            {
                _hotkey.DeviceId = device.Id;
            }
        }

        private void cmbLongDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbLongDevices.SelectedIndex == 0)
            {
                _hotkey.LongPressDeviceId = Guid.Empty;
            }
            else if (cmbLongDevices.SelectedItem is IDevice device)
            {
                _hotkey.LongPressDeviceId = device.Id;
            }
        }

        private void HotKeyForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            AudioSwitcher.Instance.DisableHotKeyFunction = false;
        }
    }
}