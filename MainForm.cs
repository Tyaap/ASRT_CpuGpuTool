using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace CpuGpuTool
{
    public partial class MainForm : Form
    {
        public CpuFile cpuFile = new CpuFile();
        public string nameFilter = "";
        public string typeFilter = "";
        public Timer listSelectTimer = new Timer();
        public string lastDirectoryPath = "";

        public MainForm()
        {
            InitializeComponent();
            listView1.ListViewItemSorter = null;
            listSelectTimer.Interval = 100;
            listSelectTimer.Tick += RefreshDetailsText;
            listSelectTimer.Tick += delegate (object sender, EventArgs e)
            {
                RefreshEntryStatus();
                button2.Enabled = button3.Enabled = listView1.SelectedIndices.Count > 0;
                listSelectTimer.Stop();
            };
        }

        static void AddListViewItems(ListView listView, ListViewItem[] items)
        {
            listView.BeginUpdate();
            listView.Items.AddRange(items);
            listView.EndUpdate();
        }

        private void RefreshDataTypeChoices()
        {
            comboBox1.Items.Clear();
            string[] items = new string[cpuFile.usedDataTypes.Count];
            int i = 0;
            foreach (DataType dataType in cpuFile.usedDataTypes)
            {
                items[i] = dataType.GetDescription();
                i++;
            }
            comboBox1.Items.AddRange(items);
        }

        private void RefreshCpuEntryList()
        {
            listView1.Items.Clear();
            int count = cpuFile.entries.Count;
            List<ListViewItem> items = new List<ListViewItem>();
            for (int i = 0; i < count; i++)
            {
                CpuEntry entry = cpuFile.entries[i];
                if (entry.name.Contains(nameFilter) && entry.dataType.GetDescription().Contains(typeFilter))
                {
                    items.Add(new ListViewItem(new string[]
                    {
                        entry.entryNumber.ToString(),
                        entry.dataType.GetDescription(),
                        entry.name
                    }));
                }
            }
            AddListViewItems(listView1, items.ToArray());
        }

        public void RefreshEntryStatus()
        {
            toolStripStatusLabel3.Text = string.Format("{0} Entries, {1} Selected", listView1.Items.Count, listView1.SelectedItems.Count);
        }

        public void RefreshDetailsText(object sender = null, EventArgs e = null)
        {
            string details = "";
            int count = Math.Min(listView1.SelectedItems.Count, 100);
            for (int i = 0; i < count; i++)
            {
                CpuEntry entry = cpuFile.entries[int.Parse(listView1.SelectedItems[i].Text) - 1];
                ListViewItem item = listView1.SelectedItems[i];
                details += string.Format(
                    "---------- ENTRY #{0} ----------\n" +
                    "Entry type: {1}\n" +
                    "Data type: {2} ({2:X})\n" +
                    "Sumo tool version: {3}\n" +
                    "Name: {4} ({5:X8})\n" +
                    "CPU header offset: 0x{6:X}\n\n",
                    entry.entryNumber, entry.entryType, entry.dataType, entry.toolVersion, entry.name, entry.id, entry.cpuOffsetHeader
                );

                details += string.Format("CPU data: {0}\n", entry.cpuDataLength > 0 ? "Yes" : "No");
                if (entry.cpuDataLength > 0)
                {
                    details += string.Format(
                        "    CPU file offset: 0x{0:X}\n" +
                        "    Size: {1} bytes\n\n",
                    entry.cpuOffsetData, entry.cpuDataLength);
                }

                details += string.Format("GPU data: {0}\n", entry.gpuDataLength > 0 ? "Yes" : "No");
                if (entry.gpuDataLength > 0)
                {
                    details += string.Format(
                        "    GPU file offset: 0x{0:X}\n" +
                        "    Size: {1} bytes",
                    entry.gpuOffsetData, entry.gpuDataLength);
                }
                details += "\n\n";
            }
            if (listView1.SelectedItems.Count > 100)
            {
                details += "More than 100 items selected, the remaining details have been omitted.";
            }
            if (listView1.SelectedItems.Count == 0)
            {
                details += "Please select a data entry.";
            }
            richTextBox1.Text = details;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog fileDialog = new OpenFileDialog() { Filter = "CPU Files|*.cpu.***", Multiselect = false, Title = "Select a CPU file", InitialDirectory = lastDirectoryPath})
            {
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    cpuFile = new CpuFile(fileDialog.FileName);
                    toolStripStatusLabel1.Text = string.Format("Current file: {0} ({1}-endian)", fileDialog.FileName, cpuFile.isLittleEndian ? "Little" : "Big");
                    RefreshCpuEntryList();
                    RefreshEntryStatus();
                    RefreshDetailsText();
                    RefreshDataTypeChoices();
                    lastDirectoryPath = Path.GetDirectoryName(fileDialog.FileName);
                }
            }
        }

        private void comboBox1_TextUpdate(object sender, EventArgs e)
        {
            typeFilter = comboBox1.Text;
            RefreshCpuEntryList();
            RefreshEntryStatus();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            nameFilter = textBox1.Text;
            RefreshCpuEntryList();
            RefreshEntryStatus();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            typeFilter = comboBox1.Text;
            RefreshCpuEntryList();
            RefreshEntryStatus();
        }

        private void comboBox1_Click(object sender, EventArgs e)
        {
            comboBox1.SelectionStart = 0;
            comboBox1.SelectionLength = comboBox1.Text.Length;
        }

        private void textBox1_Click(object sender, EventArgs e)
        {
            textBox1.SelectionStart = 0;
            textBox1.SelectionLength = textBox1.Text.Length;
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            listSelectTimer.Stop();
            listSelectTimer.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog() { Description = "Choose an output folder", SelectedPath = lastDirectoryPath })
            {
                int count = listView1.SelectedItems.Count;
                List<string> fileNames = new List<string>();
                string fileName;
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int entryIndex = int.Parse(listView1.SelectedItems[i].Text) - 1;
                        fileName = cpuFile.SaveCpuData(entryIndex, folderBrowserDialog.SelectedPath);
                        if (fileName != null)
                        {
                            fileNames.Add(fileName);
                        }
                        fileName = cpuFile.SaveGpuData(entryIndex, folderBrowserDialog.SelectedPath);
                        if (fileName != null)
                        {
                            fileNames.Add(fileName);
                        }
                    }
                    using (InfoForm form = new InfoForm())
                    {
                        form.richTextBox1.Text = "\nSuccessfully saved the following files:\n\n" + string.Join("\n", fileNames);
                        form.ShowDialog();
                    }
                    lastDirectoryPath = folderBrowserDialog.SelectedPath;
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 1)
            {
                CpuEntry entry = cpuFile.entries[int.Parse(listView1.SelectedItems[0].Text) - 1];
                using (OpenFileDialog fileDialog = new OpenFileDialog() { Multiselect = false, InitialDirectory = lastDirectoryPath })
                {
                    fileDialog.Title = "Select CPU data file";
                    bool replaceCpuData = true;
                    if (entry.entryType == EntryType.Resource)
                    {
                        replaceCpuData = MessageBox.Show("Replace CPU data?", "Replace Data", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;          
                    }
                    if (replaceCpuData && fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        cpuFile.ReplaceCpuData(entry.entryNumber - 1, fileDialog.FileName);
                        lastDirectoryPath = Path.GetDirectoryName(fileDialog.FileName);
                    }
                    fileDialog.Title = "Select GPU data file";
                    if (entry.entryType == EntryType.Resource 
                        && MessageBox.Show("Replace GPU data?", "Replace Data", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes
                        && fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        cpuFile.ReplaceGpuData(entry.entryNumber - 1, fileDialog.FileName);
                        lastDirectoryPath = Path.GetDirectoryName(fileDialog.FileName);
                    }
                }
                RefreshDetailsText();
                RefreshCpuEntryList();
            }
            else
            {
                MessageBox.Show("Please select a single entry.", "Replace Data", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
