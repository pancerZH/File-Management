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
    public delegate void deleName(String newName, FileMode fileMode, NameMode nameMode);  // 命名事件代理
    public delegate void deleSize(int size);  // 格式化事件代理
    public delegate void deleClose();  // 意外关闭窗体

    public partial class FormInput : Form
    {
        public event deleName nameEvent;  // 声明命名事件
        public event deleSize sizeEvent;  // 声明格式化事件
        public event deleClose closeEvent;  // 声明关闭窗体事件

        public FormInput(String labelText)
        {
            InitializeComponent();
            this.labelInput.Text = labelText;
        }

        private void buttonConfirm_Click(object sender, EventArgs e)
        {
            if (nameEvent != null && this.labelInput.Text.Contains("名"))
            {
                if(this.textBoxInput.Text == "")
                {
                    MessageBox.Show("名字不得为空！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (this.textBoxInput.Text.Length > 100) 
                {
                    MessageBox.Show("名字不得超过100个字符！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.textBoxInput.Text = "";
                    return;
                }
                if (this.textBoxInput.Text.IndexOf(' ') >= 0)
                {
                    MessageBox.Show("名字不得包含空格！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.textBoxInput.Text = "";
                    return;
                }
                if(this.labelInput.Text.Contains("重命名"))
                {
                    nameEvent(this.textBoxInput.Text, FileMode.unknown, NameMode.rename);  // 触发重命名事件
                    this.Close();
                    return;
                }
                if(this.labelInput.Text.Contains("目录"))
                    nameEvent(this.textBoxInput.Text, FileMode.directory, NameMode.newname);  // 触发文件夹命名事件
                else
                    nameEvent(this.textBoxInput.Text, FileMode.common, NameMode.newname);  // 触发文件命名事件
            }
            else if(sizeEvent!=null && this.labelInput.Text.Contains("MB"))
            {
                int size;
                bool result = int.TryParse(this.textBoxInput.Text, out size);
                if (result)  // 转换成功
                {
                    if (size > 100 || size < 1) 
                    {
                        MessageBox.Show("磁盘大小必须小于100MB且不小于0！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.textBoxInput.Text = "";
                        return;
                    }
                    sizeEvent(size);
                }
                else  // 转换失败
                {
                    MessageBox.Show("错误的磁盘空间设定！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.textBoxInput.Text = "";
                    return;
                }
            }
            this.Close();
        }

        //  意外关闭窗体
        private void FormInput_FormClosing(object sender, FormClosingEventArgs e)
        {
            closeEvent();
        }

        //  键盘事件
        private void FormInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)  // 按下esc键
            {
                this.Close();
                closeEvent?.Invoke();
            }
            if (e.KeyCode == Keys.Enter)  // 按下回车键
                buttonConfirm_Click(sender, e);
        }
    }
}
