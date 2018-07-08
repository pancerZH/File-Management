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
    public partial class FormProfile : Form
    {
        public event deleClose closeEvent;  // 声明关闭窗体事件

        public FormProfile(FormMain.Inode inode)
        {
            InitializeComponent();
            String createdTime = "创建时间：" + inode.createdTime.ToString();
            String updatedTime = "上次修改时间：" + inode.updatedTime.ToString();
            String fileMode = "文件类型：" + inode.fileMode.ToString();
            String size = "文件大小：" + inode.fileSize.ToString() + " Bytes";
            String children = "子文件数量：" + inode.childrenNum.ToString();
            String profile;
            if (inode.fileMode == FileMode.common)  // 普通文件不显示子文件数量
                profile = createdTime + "\r\n" + updatedTime + "\r\n" + fileMode + "\r\n" + size;
            else  // 目录文件显示子文件数量
                profile = createdTime + "\r\n" + updatedTime + "\r\n" + fileMode + "\r\n" + size + "\r\n" + children;
            labelProfile.Text = profile;
        }

        //  按下空格键，关闭窗口
        private void FormProfile_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                closeEvent?.Invoke();
                this.Close();
            }
        }

        //  意外关闭
        private void FormProfile_FormClosing(object sender, FormClosingEventArgs e)
        {
            closeEvent?.Invoke();
        }
    }
}
