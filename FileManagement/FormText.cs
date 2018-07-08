using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileManagement
{
    public delegate void deleChange(bool success, String content);  // 修改文件事件代理

    public partial class FormText : Form
    {
        public event deleChange changeEvent;  // 声明修改文件事件
        public event deleClose closeEvent;  // 声明关闭窗体事件

        public FormText(bool writable, String content)
        {
            InitializeComponent();
            if (writable)  // 可以写入
                this.Enabled = true;
            else  // 不可写入
            {
                this.textBoxFile.ReadOnly = true;
                this.buttonConfirm.Enabled = false;
            }
            textBoxFile.Text = content;
        }

        private void buttonConfirm_Click(object sender, EventArgs e)
        {
            changeEvent?.Invoke(true, textBoxFile.Text);  // 保存更改
            this.Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            changeEvent?.Invoke(false, "");  // 不保存修改
            this.Close();
        }

        //  键盘事件
        private void FormText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)  // 按下esc键时
                buttonCancel_Click(sender, e);
            if (e.KeyCode == Keys.S && e.Control && buttonConfirm.Enabled)  // Control+s保存
                buttonCancel_Click(sender, e);
        }

        //  意外关闭窗体
        private void FormText_FormClosing(object sender, FormClosingEventArgs e)
        {
            closeEvent?.Invoke();
        }
    }
}
