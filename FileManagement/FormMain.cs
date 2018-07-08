using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;


namespace FileManagement
{
    public enum FileMode : byte { unknown, common, directory };  // 文件类型
    public enum NameMode : byte { rename, newname};  // 重命名还是新命名？

    public partial class FormMain : Form
    {
        public const int BLOCKSIZE = 4096;  // 数据块大小为4096Bytes
        public const int INODENUM = 2048;  // 一个块组中inode的数量上限为2048个
        private BlockGroup blockGroup = null;
        public struct SuperBlock  // 超级块
        {
            public int inodeNum;
            public int freeInodeNum;
        }
        public struct Inode  // inode
        {
            public FileMode fileMode;  // 文件类型
            public int fileSize;  // 文件大小
            public String fileName;  // 文件名
            public DateTime createdTime;  // 创建时间
            public DateTime updatedTime;  // 修改时间
            public int blockSize;  // 占块数
            public List<int> dataBlockList;  // 数据块占用的数据块组
            public List<int> childInodeIndex;  // 记录子文件
            public int childrenNum;  // 子文件的数量
            public int fatherIndex;  // 父目录的编号
        }
        public struct DataBlock  // 数据块
        {
            public char[] data;  // 一个数据块可以存储2048个Unicode字符
        }
        public struct GroupDescriptor  // 组描述符
        {
            public Dictionary<int, bool> blockBitmap;  // 块位图，true代表空闲，false代表已使用
            public Dictionary<int, bool> inodeBitmap;  // inode位图
        }



        public class BlockGroup  // 数据结构：块组
        {
            public int volume;  // 磁盘大小
            public SuperBlock superBlock;  // 超级块
            public List<GroupDescriptor> groupDescriptorList;  // 组描述符表
            public List<DataBlock> dataBlockList;  // 所有数据块列表
            public List<Inode> inodeList;  // 所有inode列表
            public int currentInodeIndex;

            public BlockGroup(int size, int inodeNum)  // 格式化时使用
            {
                volume = size;
                int sizeInByte = size * 1024 * 1024;
                int blockNum = sizeInByte / BLOCKSIZE;  // 数据块的数量
                superBlock.inodeNum = inodeNum;
                superBlock.freeInodeNum = inodeNum;
                inodeList = new List<Inode>();
                GroupDescriptor groupDescriptor;
                groupDescriptor.blockBitmap = new Dictionary<int, bool>();
                groupDescriptor.inodeBitmap = new Dictionary<int, bool>();
                for (int i = 0; i < inodeNum; i++) 
                {
                    groupDescriptor.inodeBitmap.Add(i, true);
                    Inode inode;
                    inode.childrenNum = 0;
                    inode.fileMode = FileMode.unknown;
                    inode.fileSize = 0;
                    inode.fileName = "";
                    inode.createdTime = new DateTime();
                    inode.updatedTime = new DateTime();
                    inode.blockSize = 0;
                    inode.dataBlockList = new List<int>();
                    inode.childInodeIndex = new List<int>();
                    inode.fatherIndex = -1;  // 无父目录
                    inodeList.Add(inode);
                }
                groupDescriptorList = new List<GroupDescriptor>();
                groupDescriptorList.Add(groupDescriptor);
                dataBlockList = new List<DataBlock>();
                for (int i = 0; i < blockNum; i++)
                {
                    DataBlock dataBlock;
                    dataBlock.data = new char[BLOCKSIZE / 2];
                    dataBlock.data.Initialize();
                    dataBlockList.Add(dataBlock);
                    groupDescriptor.blockBitmap.Add(i, true);
                }

                currentInodeIndex = -1;  // 无父目录
                if(!createFile(FileMode.directory, "root"))  // 建立root目录
                    MessageBox.Show("磁盘空间不足！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                currentInodeIndex = 0;  // 当前目录：root
            }

            //  将目录文件写入虚拟磁盘
            public bool writeDirectoryFileToDisk(int directoryIndex)
            {
                Inode dirInode = inodeList[directoryIndex];
                int childIndex = 0, blockNum;
                for (blockNum = 0; blockNum < dirInode.dataBlockList.Count; blockNum++)  // 遍历目录占用的数据块
                {
                    DataBlock block = dataBlockList[dirInode.dataBlockList[blockNum]];
                    int blockPoint = 0;
                    for (; childIndex < dirInode.childInodeIndex.Count; childIndex++)  // 遍历目录包含的文件
                    {
                        String index = dirInode.childInodeIndex[childIndex].ToString();
                        if (BLOCKSIZE / 2 - blockPoint >= index.Length) 
                        {
                            for (int j = 0; j < index.Length; j++)  // 逐字写入
                            {
                                block.data[blockPoint] = index[j];
                                blockPoint++;
                            }
                            block.data[blockPoint] = '\0';
                            blockPoint++;
                        }
                    }
                    for (; blockPoint < BLOCKSIZE / 2; blockPoint++)
                        block.data[blockPoint] = '\0';
                    dataBlockList[dirInode.dataBlockList[blockNum]] = block;
                    if (childIndex == dirInode.childInodeIndex.Count)  // 已经遍历完了所有的文件
                    {
                        blockNum++;
                        break;
                    }
                }
                //  遍历之后有两种结果：原始块不足或剩余
                int count = dirInode.dataBlockList.Count;
                if (childIndex < dirInode.childInodeIndex.Count || (childIndex == 0) && dirInode.childrenNum != 0)   // 原始块不足
                {
                    int indexOfBlock = -1;
                    foreach (KeyValuePair<int, bool> kvp in groupDescriptorList[0].blockBitmap)  // 找到一个空闲数据块
                    {
                        if (kvp.Value == true)
                        {
                            indexOfBlock = kvp.Key;
                            break;
                        }
                    }
                    if (indexOfBlock == -1)  // 没有足够的数据块
                    {
                        updateInodeInfo(ref dirInode);
                        inodeList[directoryIndex] = dirInode;  // 写回
                        return false;
                    }
                    groupDescriptorList[0].blockBitmap[indexOfBlock] = false;
                    String index = dirInode.childInodeIndex[childIndex].ToString();
                    DataBlock block = dataBlockList[indexOfBlock];
                    for (int j = 0; j < index.Length; j++)  // 逐字写入
                        block.data[j] = index[j];
                    dirInode.dataBlockList.Add(indexOfBlock);  // 添加新的数据块的索引
                    dataBlockList[indexOfBlock] = block;  // 写入数据块
                }
                if (blockNum < count || dirInode.childrenNum == 0)    // 原始块剩余
                {
                    for (; blockNum < dirInode.dataBlockList.Count; blockNum++) 
                    {
                        int freeIndex = dirInode.dataBlockList[blockNum];  // 要释放的数据块的index
                        groupDescriptorList[0].blockBitmap[freeIndex] = true;
                        for (int i = 0; i < BLOCKSIZE / 2; i++)
                            dataBlockList[freeIndex].data[i] = '\0';
                        dirInode.dataBlockList.Remove(freeIndex);  // 从子列表中移除
                    }
                }
                updateInodeInfo(ref dirInode);
                inodeList[directoryIndex] = dirInode;  // 写回
                return true;
            }

            //  目录文件从磁盘数据块读取数据
            public void readDirectoryFileFromDisk(ref Inode inode)
            {
                foreach(var i in inode.dataBlockList)  // 逐个读取数据块
                {
                    DataBlock dataBlock = dataBlockList[i];  // 一个数据块
                    String data = new string(dataBlock.data);
                    var indexGroup = data.Split(new char[] { '\0' }).Take(inode.childrenNum);
                    foreach (var indexStr in indexGroup)
                    {
                        int index;
                        bool result = int.TryParse(indexStr, out index);
                        if (result == true)  // 转换成功
                            inode.childInodeIndex.Add(index);
                    }
                }
            }

            //  将普通文件写入虚拟磁盘
            public bool writeCommonFileToDisk(int commonIndex, String content)
            {
                Inode inode = inodeList[commonIndex];
                int contentPoint = 0;  // 文件内容指针
                int blockNum;
                for (blockNum = 0; blockNum < inode.dataBlockList.Count; blockNum++)  // 遍历文件占用的数据块
                {
                    DataBlock dataBlock = dataBlockList[inode.dataBlockList[blockNum]];
                    for(int i=0; i<BLOCKSIZE/2; i++)  // 将内容写入
                    {
                        if (contentPoint < content.Length)
                            dataBlock.data[i] = content[contentPoint];
                        else
                            dataBlock.data[i] = '\0';
                        contentPoint++;
                    }
                }
                //  原始块不足或剩余
                int count = inode.dataBlockList.Count;
                for (; contentPoint < content.Length;)    // 原始块不足
                {
                    int indexOfBlock = -1;
                    foreach (KeyValuePair<int, bool> kvp in groupDescriptorList[0].blockBitmap)  // 找到一个空闲数据块
                    {
                        if (kvp.Value == true)
                        {
                            indexOfBlock = kvp.Key;
                            break;
                        }
                    }
                    if (indexOfBlock == -1)  // 没有足够的数据块
                    {
                        updateInodeInfo(ref inode);
                        inodeList[commonIndex] = inode;
                        return false;
                    }
                    groupDescriptorList[0].blockBitmap[indexOfBlock] = false;
                    DataBlock dataBlock = dataBlockList[indexOfBlock];
                    for (int i = 0; i < BLOCKSIZE / 2 && contentPoint < content.Length; i++) 
                    {
                        dataBlock.data[i] = content[contentPoint];
                        contentPoint++;
                    }
                    inode.dataBlockList.Add(indexOfBlock);  // 添加索引
                    dataBlockList[indexOfBlock] = dataBlock;  // 写回
                }
                for (; blockNum < count; blockNum++)  // 原始块剩余
                {
                    int freeIndex = inode.dataBlockList[blockNum];  // 要释放的数据块的index
                    groupDescriptorList[0].blockBitmap[freeIndex] = true;
                    for (int i = 0; i < BLOCKSIZE / 2; i++)
                        dataBlockList[freeIndex].data[i] = '\0';
                    inode.dataBlockList.Remove(freeIndex);  // 从子列表中移除
                }
                updateInodeInfo(ref inode);
                inodeList[commonIndex] = inode;  // 写回
                return true;
            }

            //  普通文件从磁盘读取数据
            public String readCommonFileFromDisk(int commonIndex)
            {
                Inode inode = inodeList[commonIndex];
                String content = "";
                foreach(int index in inode.dataBlockList)
                {
                    DataBlock dataBlock = dataBlockList[index];  // 一个数据块
                    String data = new string(dataBlock.data);
                    content += data;
                }
                return content;
            }

            //  展示当前目录下的文件
            public void showFile(ref ListBox listBox)
            {
                listBox.Items.Clear();
                foreach (var nodeIndex in inodeList[currentInodeIndex].childInodeIndex)
                {
                    String fileType;
                    if (inodeList[nodeIndex].fileMode == FileMode.common)
                        fileType = "文件";
                    else if (inodeList[nodeIndex].fileMode == FileMode.directory)
                        fileType = "目录";
                    else
                        fileType = "未知";
                    listBox.Items.Add(inodeList[nodeIndex].fileName+"             "+fileType);
                }
            }

            //  在当前目录下创建文件
            public bool createFile(FileMode fileMode, String fileName)
            {
                // 获取inode
                if (superBlock.freeInodeNum == 0)
                    return false;
                superBlock.freeInodeNum--;
                int indexOfInode = -1;
                foreach(KeyValuePair<int, bool> kvp in groupDescriptorList[0].inodeBitmap)
                {
                    if(kvp.Value == true)
                    {
                        indexOfInode = kvp.Key;
                        break;
                    }
                }
                if (indexOfInode == -1)
                    return false;
                groupDescriptorList[0].inodeBitmap[indexOfInode] = false;
                Inode inode = inodeList[indexOfInode];

                // 创建文件，改写inode
                inode.fileMode = fileMode;
                inode.fileName = fileName;
                inode.fatherIndex = currentInodeIndex;
                inode.createdTime = DateTime.Now;
                inode.updatedTime = inode.createdTime;
                inodeList[indexOfInode] = inode;
                // 为父目录添加记录
                if (currentInodeIndex == -1)  // 无父目录
                    return true;
                inodeList[currentInodeIndex].childInodeIndex.Add(indexOfInode);
                Inode dirInode = inodeList[currentInodeIndex];
                dirInode.childrenNum++;
                inodeList[currentInodeIndex] = dirInode;
                if(!writeDirectoryFileToDisk(currentInodeIndex))
                    return false;
                return true;
            }

            //  删除选中的文件
            public void deleteFile(String fileName)
            {
                int deleteFileIndex = getIndexFromeName(fileName, currentInodeIndex);
                if (deleteFileIndex == -1)
                    return;
                int tempCurrentInodeIndex = currentInodeIndex;
                Inode deleteInode = inodeList[deleteFileIndex];
                if (inodeList[deleteFileIndex].childrenNum == 0)  // 普通文件和空目录直接删除
                {
                    foreach (var index in deleteInode.dataBlockList)
                        groupDescriptorList[0].blockBitmap[index] = true;  // 释放占用的数据块
                    groupDescriptorList[0].inodeBitmap[deleteFileIndex] = true;  // 释放占用的inode块
                    //MessageBox.Show(inodeList[deleteFileIndex].fileName, "", MessageBoxButtons.OK, MessageBoxIcon.Error);  // 检查删除顺序
                    inodeList[currentInodeIndex].childInodeIndex.Remove(deleteFileIndex);
                    //  抹去inode信息和数据块内容
                    deleteInode.blockSize = 0;
                    foreach(var index in deleteInode.dataBlockList)
                    {
                        DataBlock dataBlock = dataBlockList[index];
                        for (int i = 0; i < BLOCKSIZE / 2; i++)
                            dataBlock.data[i] = '\0';
                        dataBlockList[index] = dataBlock;
                    }
                    deleteInode.dataBlockList.Clear();
                    deleteInode.fatherIndex = -1;
                    deleteInode.fileSize = 0;
                    superBlock.freeInodeNum++;
                    inodeList[deleteFileIndex] = deleteInode;  // 写回
                    Inode fatherInode = inodeList[tempCurrentInodeIndex];
                    fatherInode.childrenNum--;
                    inodeList[tempCurrentInodeIndex] = fatherInode;  // 写回
                    writeDirectoryFileToDisk(tempCurrentInodeIndex);
                }
                else  // 有子文件的目录文件递归删除
                {
                    int childrenNum = deleteInode.childrenNum;
                    for (int i = 0; i < childrenNum; i++)  // 遍历目录文件的子文件索引
                    {
                        currentInodeIndex = deleteFileIndex;  // 将当前目录切换到要删除的目录下
                        int childIndex = deleteInode.childInodeIndex[0];
                        String childName = inodeList[childIndex].fileName;
                        deleteFile(childName);
                    }
                    currentInodeIndex = tempCurrentInodeIndex;
                    deleteFile(fileName);
                }
            }

            //  在father目录下按名字查找inode索引
            public int getIndexFromeName(String name, int fatherIndex)
            {
                if (name == "root" && fatherIndex == -1)
                    return 0;
                int fileIndex = -1;
                foreach (var i in inodeList[fatherIndex].childInodeIndex)
                {
                    if (inodeList[i].fileName == name)
                    {
                        fileIndex = i;
                        break;
                    }
                }
                return fileIndex;
            }

            //  计算文件大小
            public int calculateFileSize(String name, int fatherIndex)
            {
                int fileIndex = getIndexFromeName(name, fatherIndex);
                Inode inode = inodeList[fileIndex];
                int size = 0;
                foreach(var i in inode.dataBlockList)
                {
                    DataBlock dataBlock = dataBlockList[i];
                    String data = new string(dataBlock.data);
                    if (inode.fileMode == FileMode.directory)  // 目录
                        foreach (var indexStr in data.Split(new char[] { '\0' }).Take(inode.childrenNum))
                            size += indexStr.Length;
                    else  // 文件
                        size += data.Split(new char[] { '\0' })[0].Length;
                }
                return size*2;
            }

            //  更新文件信息
            public void updateInodeInfo(ref Inode inode)
            {
                inode.blockSize = inode.dataBlockList.Count;
                inode.updatedTime = DateTime.Now;
                inode.fileSize = calculateFileSize(inode.fileName, inode.fatherIndex);
            }
        }



        //  从真实磁盘读入文件系统
        public bool readFileSystemFromRealDisk()
        {
            try
            {
                FileStream fsTest = new FileStream("disk.img", System.IO.FileMode.Open, FileAccess.Read);
                fsTest.Close();
            }
            catch (FileNotFoundException)  // 未找到文件
            {
                MessageBox.Show("未找到磁盘映像！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            FileStream fs = new FileStream("disk.img", System.IO.FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs);
            String volumnStr = sr.ReadLine();  // 读入卷大小
            blockGroup = new BlockGroup(int.Parse(volumnStr), INODENUM);  // 建立文件系统
            blockGroup.volume = int.Parse(volumnStr);
            String superBlockStr = sr.ReadLine();  // 读入超级块
            blockGroup.superBlock.inodeNum = int.Parse(superBlockStr.Split(' ')[0]);
            blockGroup.superBlock.freeInodeNum = int.Parse(superBlockStr.Split(' ')[1]);
            String blockBitMapStr = sr.ReadLine();  // 读入数据块位图
            foreach (var index in blockBitMapStr.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                blockGroup.groupDescriptorList[0].blockBitmap[int.Parse(index)] = false;
            String inodeBitMapStr = sr.ReadLine();  // 读入inode位图
            foreach (var index in inodeBitMapStr.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                blockGroup.groupDescriptorList[0].inodeBitmap[int.Parse(index)] = false;
            //  逐个读入数据块
            foreach (var i in blockBitMapStr.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int index = int.Parse(i);
                DataBlock dataBlock = new DataBlock();
                dataBlock.data = new char[BLOCKSIZE / 2];
                sr.ReadBlock(dataBlock.data, 0, BLOCKSIZE / 2);
                blockGroup.dataBlockList[index] = dataBlock;
            }
            sr.ReadLine();
            //  逐个读入inode
            foreach (var i in inodeBitMapStr.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int index = int.Parse(i);
                Inode inode = new Inode();
                String inodeStr = sr.ReadLine();
                var strList = inodeStr.Split(' ');
                //  读入文件类型
                if (strList[0] == FileMode.directory.ToString())
                    inode.fileMode = FileMode.directory;
                else if (strList[0] == FileMode.common.ToString())
                    inode.fileMode = FileMode.common;
                else
                    inode.fileMode = FileMode.unknown;
                //  读入文件大小
                inode.fileSize = int.Parse(strList[1]);
                //  读入文件名称
                inode.fileName = strList[2];
                //  读入创建时间
                inode.createdTime = DateTime.Parse(strList[3] + ' ' + strList[4]);
                //  读入更新时间
                inode.updatedTime = DateTime.Parse(strList[5] + ' ' + strList[6]);
                //  读入占块数
                inode.blockSize = int.Parse(strList[7]);
                //  读入占用的数据块的索引
                inode.dataBlockList = new List<int>();
                foreach (var j in strList[8].Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                    inode.dataBlockList.Add(int.Parse(j));
                //  读入子文件的inode索引
                inode.childInodeIndex = new List<int>();
                foreach (var j in strList[9].Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                    inode.childInodeIndex.Add(int.Parse(j));
                //  读入子文件数量
                inode.childrenNum = int.Parse(strList[10]);
                //  读入父目录编号
                inode.fatherIndex = int.Parse(strList[11]);
                //  写入inode
                blockGroup.inodeList[index] = inode;
            }
            sr.Close();
            fs.Close();
            return true;
        }



        public FormMain()
        {
            InitializeComponent();
            bool existImg = true;
            try
            {
                FileStream fsTest = new FileStream("disk.img", System.IO.FileMode.Open, FileAccess.Read);
                fsTest.Close();
            }
            catch (FileNotFoundException)  // 未找到文件
            {
                MessageBox.Show("未找到磁盘映像！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                existImg = false;
            }
            if (existImg == false)  // 没有磁盘映像
                return;
            readFileSystemFromRealDisk();  // 读入文件系统
            blockGroup.showFile(ref listBoxFile);
            directorySet();
        }

        //  打开文件（只读）
        private void buttonOpen_Click(object sender, EventArgs e)
        {
            if (blockGroup == null)
            {
                MessageBox.Show("请先格式化！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var selectedItem = listBoxFile.SelectedItem;
            if (selectedItem == null)
                return;
            String selectedName = selectedItem.ToString().Split(new char[] { ' ' })[0];
            int selectedFileIndex = blockGroup.getIndexFromeName(selectedName, blockGroup.currentInodeIndex);
            if (selectedFileIndex == -1 || blockGroup.inodeList[selectedFileIndex].fileMode != FileMode.common) 
                return;
            FormText fm = new FormText(false, blockGroup.readCommonFileFromDisk(selectedFileIndex));
            fm.changeEvent += new deleChange(changeEventHandler);
            fm.closeEvent += new deleClose(closeEventHandler);
            fm.Show();
            this.Enabled = false;
        }

        //  写文件（读写）
        private void buttonWrite_Click(object sender, EventArgs e)
        {
            if (blockGroup == null)
            {
                MessageBox.Show("请先格式化！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var selectedItem = listBoxFile.SelectedItem;
            if (selectedItem == null)
                return;
            String selectedName = selectedItem.ToString().Split(new char[] { ' ' })[0];
            int selectedFileIndex = blockGroup.getIndexFromeName(selectedName, blockGroup.currentInodeIndex);
            if (selectedFileIndex == -1 || blockGroup.inodeList[selectedFileIndex].fileMode != FileMode.common)
                return;
            FormText fm = new FormText(true, blockGroup.readCommonFileFromDisk(selectedFileIndex));
            fm.changeEvent += new deleChange(changeEventHandler);
            fm.closeEvent += new deleClose(closeEventHandler);
            fm.Show();
            this.Enabled = false;
        }

        //  格式化
        private void buttonFormat_Click(object sender, EventArgs e)
        {
            FormInput fm = new FormInput("请输入磁盘空间（单位：MB）");
            fm.sizeEvent += new deleSize(sizeEventHandler);  // 添加事件
            fm.closeEvent += new deleClose(closeEventHandler);
            fm.Show();
            this.Enabled = false;
            directorySet();
        }


        //  创建文件
        private void buttonCreate_Click(object sender, EventArgs e)
        {
            if (blockGroup == null)
            {
                MessageBox.Show("请先格式化！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            FormInput fm = new FormInput("请输入新文件名");
            fm.nameEvent += new deleName(nameEventHandler);  // 添加事件
            fm.closeEvent += new deleClose(closeEventHandler);
            fm.Show();
            this.Enabled = false;
        }

        //  重命名
        private void buttonRename_Click(object sender, EventArgs e)
        {
            if (blockGroup == null)
            {
                MessageBox.Show("请先格式化！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            FormInput fm = new FormInput("请输入新名称（重命名）");
            fm.nameEvent += new deleName(nameEventHandler);  // 添加事件
            fm.closeEvent += new deleClose(closeEventHandler);
            fm.Show();
            this.Enabled = false;
        }

        //  创建目录
        private void buttonCreateDirectory_Click(object sender, EventArgs e)
        {
            if (blockGroup == null)
            {
                MessageBox.Show("请先格式化！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            FormInput fm = new FormInput("请输入新目录名");
            fm.nameEvent += new deleName(nameEventHandler);  // 添加事件
            fm.closeEvent += new deleClose(closeEventHandler);
            fm.Show();
            this.Enabled = false;
        }

        //  关闭系统，保存磁盘镜像
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.Close();
            if(blockGroup != null)
            {
                FileStream fs = new FileStream("disk.img", System.IO.FileMode.Create, FileAccess.ReadWrite);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine(blockGroup.volume);  // 写入卷大小
                sw.WriteLine(blockGroup.superBlock.inodeNum.ToString() + ' ' + blockGroup.superBlock.freeInodeNum.ToString());  // 写入超级块
                //  写入组描述符
                String bbm = "";
                foreach(var kvp in blockGroup.groupDescriptorList[0].blockBitmap)
                    if (kvp.Value == false)
                        bbm = bbm + kvp.Key.ToString() + ' ';
                sw.WriteLine(bbm);  // 写入数据块位图
                String ibm = "";
                foreach(var kvp in blockGroup.groupDescriptorList[0].inodeBitmap)
                    if(kvp.Value == false)
                        ibm = ibm + kvp.Key.ToString() + ' ';
                sw.WriteLine(ibm);  // 写入inode位图
                foreach (var kvp in blockGroup.groupDescriptorList[0].blockBitmap)  // 写入数据块
                {
                    if(kvp.Value == false)
                    {
                        String data = new string(blockGroup.dataBlockList[kvp.Key].data);
                        sw.Write(data);
                        //sw.WriteLine(data);
                    }
                }
                sw.WriteLine();
                foreach (var kvp in blockGroup.groupDescriptorList[0].inodeBitmap)  // 写入inode块
                {
                    if(kvp.Value == false)
                    {
                        String data = "";
                        Inode inode = blockGroup.inodeList[kvp.Key];
                        data = inode.fileMode.ToString() + ' ' + inode.fileSize.ToString() + ' ' + inode.fileName + ' '
                            + inode.createdTime.ToString() + ' ' + inode.updatedTime.ToString() + ' ' + inode.blockSize.ToString() + ' ';
                        foreach (int index in inode.dataBlockList)
                            data = data + index.ToString() + '/';
                        data += ' ';
                        foreach (int index in inode.childInodeIndex)
                            data = data + index.ToString() + '/';
                        data = data + ' ' + inode.childrenNum.ToString() + ' ' + inode.fatherIndex.ToString();
                        sw.WriteLine(data);
                    }
                }
                sw.Close();
                fs.Close();
            }
        }

        //  删除文件
        private void buttonDelete_Click(object sender, EventArgs e)
        {
            if (blockGroup == null)
            {
                MessageBox.Show("请先格式化！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var selectedItem = listBoxFile.SelectedItem;
            String selectedName = selectedItem.ToString().Split(new char[] { ' ' })[0];
            blockGroup.deleteFile(selectedName);
            blockGroup.showFile(ref listBoxFile);
        }

        //  进入下级目录
        private void buttonForward_Click(object sender, EventArgs e)
        {
            if (blockGroup == null)
            {
                MessageBox.Show("请先格式化！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var selectedItem = listBoxFile.SelectedItem;
            if (selectedItem == null)
                return;
            String selectedName = selectedItem.ToString().Split(new char[] { ' ' })[0];
            int indexOfSelectedInode = blockGroup.getIndexFromeName(selectedName, blockGroup.currentInodeIndex);
            if (indexOfSelectedInode == -1 || blockGroup.inodeList[indexOfSelectedInode].fileMode != FileMode.directory)
                return;
            //blockGroup.inodeList[blockGroup.currentInodeIndex].childInodeIndex.Clear();  // 清除
            blockGroup.currentInodeIndex = indexOfSelectedInode;
            if(blockGroup.inodeList[blockGroup.currentInodeIndex].childInodeIndex.Count == 0)
            {
                Inode dirInode = blockGroup.inodeList[blockGroup.currentInodeIndex];
                blockGroup.readDirectoryFileFromDisk(ref dirInode);
                blockGroup.inodeList[blockGroup.currentInodeIndex] = dirInode;
            }
            blockGroup.showFile(ref listBoxFile);
            directorySet();
        }
        //  将listBox的双击事件与进入下级目录事件和打开文件事件绑定
        private void listBoxFile_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var selectedItem = listBoxFile.SelectedItem;
            if (selectedItem == null || blockGroup == null)
                return;
            if(selectedItem.ToString().Contains("目录"))
                buttonForward_Click(sender, e);
            else
                buttonOpen_Click(sender, e);
        }

        //  返回上级目录
        private void buttonBack_Click(object sender, EventArgs e)
        {
            if (blockGroup == null)
            {
                MessageBox.Show("请先格式化！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Inode inode = blockGroup.inodeList[blockGroup.currentInodeIndex];
            if (inode.fatherIndex == -1)
                return;
            //blockGroup.inodeList[blockGroup.currentInodeIndex].childInodeIndex.Clear();  // 清除
            blockGroup.currentInodeIndex = inode.fatherIndex;
            if (blockGroup.inodeList[blockGroup.currentInodeIndex].childInodeIndex.Count == 0)
            {
                Inode dirInode = blockGroup.inodeList[blockGroup.currentInodeIndex];
                blockGroup.readDirectoryFileFromDisk(ref dirInode);
                blockGroup.inodeList[blockGroup.currentInodeIndex] = dirInode;
            }
            blockGroup.showFile(ref listBoxFile);
            directorySet();
        }

        //  键盘事件
        private void FormMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (blockGroup != null)
            {
                var selectedItem = listBoxFile.SelectedItem;
                if (selectedItem != null)
                {
                    if (e.KeyCode == Keys.Space)  // 按下空格键时
                    {
                        String fileName = selectedItem.ToString().Split(new char[] { ' ' })[0];
                        int fileIndex = blockGroup.getIndexFromeName(fileName, blockGroup.currentInodeIndex);
                        FormProfile fm = new FormProfile(blockGroup.inodeList[fileIndex]);
                        fm.Show();
                        fm.closeEvent += new deleClose(closeEventHandler);
                        this.Enabled = false;
                    }
                    else if (e.KeyCode == Keys.Enter)  // 按下回车键时
                    {
                        if (selectedItem.ToString().Contains("目录"))
                            buttonForward_Click(sender, e);
                        else
                            buttonOpen_Click(sender, e);
                    }
                    else if (e.KeyCode == Keys.Delete)  // 按下删除键时
                    {
                        buttonDelete_Click(sender, e);
                    }
                    else if (e.KeyCode == Keys.R)  // 按下r键时
                    {
                        buttonRename_Click(sender, e);
                    }
                    else if (e.KeyCode == Keys.W && !e.Control)  // 按下w键时
                    {
                        buttonWrite_Click(sender, e);
                    }
                }
                if (e.KeyCode == Keys.Back)  // 按下退格键时
                {
                    buttonBack_Click(sender, e);
                }
                else if (e.KeyCode == Keys.N)  // 按下n键时
                {
                    buttonCreate_Click(sender, e);
                }
                else if (e.KeyCode == Keys.D)  // 按下d键时
                {
                    buttonCreateDirectory_Click(sender, e);
                }
                else if (e.KeyCode == Keys.F)  // 按下f键时
                {
                    buttonFormat_Click(sender, e);
                }
                else if (e.KeyCode == Keys.W && e.Control)  // 按下control+w键时
                {
                    buttonCancel_Click(sender, e);
                }
            }
        }

        //  为不同属性文件染色
        private void listBoxFile_DrawItem(object sender, DrawItemEventArgs e)
        {
            Brush fontBrush = Brushes.Blue;
            ListBox listBox = sender as ListBox;
            if(e.Index >= 0)
            {
                String itemText = listBox.Items[e.Index].ToString();
                var itemList = itemText.Split(new char[] { ' ' });
                e.DrawBackground();
                switch (itemList[itemList.Length-1])
                {
                    case "目录":
                        e.Graphics.DrawString(itemText, e.Font, Brushes.Blue, e.Bounds);
                        break;
                    case "文件":
                        e.Graphics.DrawString(itemList[0], e.Font, Brushes.Black, e.Bounds);
                        break;
                    default:
                        e.Graphics.DrawString(itemList[0], e.Font, Brushes.Red, e.Bounds);
                        break;
                }
                e.DrawFocusRectangle();
            }
        }

        //  设置当前路径
        public void directorySet()
        {
            if (blockGroup == null)
                return;
            List<string> directoryList = new List<string>();
            int currentInode = blockGroup.currentInodeIndex;  // 获取当前的目录
            while (currentInode != -1)  // 一直回溯到根目录下
            {
                directoryList.Add(blockGroup.inodeList[currentInode].fileName);
                currentInode = blockGroup.inodeList[currentInode].fatherIndex;
            }
            directoryList.Reverse();
            labelDirectory.Text = "";
            foreach (var i in directoryList)
                labelDirectory.Text = labelDirectory.Text + i + '/';
        }

        //  命名事件处理函数
        public void nameEventHandler(String newName, FileMode fileMode, NameMode nameMode)
        {
            foreach (var nodeIndex in blockGroup.inodeList[blockGroup.currentInodeIndex].childInodeIndex)
            {
                if (blockGroup.inodeList[nodeIndex].fileName == newName)
                {
                    MessageBox.Show("存在重名文件！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            if (nameMode == NameMode.rename)  // 重命名
            {
                var selectedItem = listBoxFile.SelectedItem;
                String selectedName = selectedItem.ToString().Split(new char[] { ' ' })[0];
                int fileIndex = blockGroup.getIndexFromeName(selectedName, blockGroup.currentInodeIndex);
                if (fileIndex == -1)
                    return;
                else
                {
                    Inode inode = blockGroup.inodeList[fileIndex];
                    inode.fileName = newName;
                    blockGroup.inodeList[fileIndex] = inode;
                }
                blockGroup.showFile(ref listBoxFile);
                this.Enabled = true;
                return;
            }
            if (!blockGroup.createFile(fileMode, newName)) 
                MessageBox.Show("磁盘空间不足！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);

            blockGroup.showFile(ref this.listBoxFile);
            this.Enabled = true;
        }

        //  格式化事件处理函数
        public void sizeEventHandler(int size)
        {
            blockGroup = new BlockGroup(size, INODENUM);
            this.listBoxFile.Items.Clear();
            directorySet();
            this.Enabled = true;
            /*  测试文件数据保存在多个数据块中的情况
            String newName = "a";
            for(int i=0; i<1000; i++)
            {
                create(FileMode.common, newName);
                newName += "a";
            }
            blockGroup.showFile(ref this.listBoxFile);*/
        }

        //  文件修改事件处理函数
        public void changeEventHandler(bool success, String content)
        {
            this.Enabled = true;
            if(success == true)  // 保存修改
            {
                var selectedItem = listBoxFile.SelectedItem;
                String selectedName = selectedItem.ToString().Split(new char[] { ' ' })[0];
                int selectedFileIndex = blockGroup.getIndexFromeName(selectedName, blockGroup.currentInodeIndex);
                if (selectedFileIndex == -1)
                    return;
                if(!blockGroup.writeCommonFileToDisk(selectedFileIndex, content))
                    MessageBox.Show("磁盘空间不足！", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //  意外关闭事件处理函数
        public void closeEventHandler()
        {
            this.Enabled = true;
        }
    }
}
