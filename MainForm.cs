using System;
using System.Collections.Generic;
using System.Globalization;
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
                button2.Enabled = listView1.SelectedIndices.Count > 0;
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
            int count = cpuFile.entriesList.Count;
            List<ListViewItem> items = new List<ListViewItem>();
            for (int i = 0; i < count; i++)
            {
                CpuEntry entry = cpuFile.entriesList[i];
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
            List<int> linkPositions = new List<int>();
            int count = Math.Min(listView1.SelectedItems.Count, 100);
            for (int i = 0; i < count; i++)
            {
                CpuEntry entry = cpuFile.entriesList[int.Parse(listView1.SelectedItems[i].Text) - 1];
                ListViewItem item = listView1.SelectedItems[i];

                details += string.Format("------------ ENTRY #{0} ------------", entry.entryNumber);
                details += entry.entryType == EntryType.Node ? "\nSumo Engine Node" : "\nSumo Loader Resource";
                details += string.Format("\nType: {0} ({0:X})", entry.dataType);
                details += string.Format("\nEntry ID: {0:X8}", entry.id);
                details += string.Format("\nName: {0}", entry.name, entry.id);
                if (!string.IsNullOrEmpty(entry.shortName))
                {
                    details += string.Format("\nShort name: {0}", entry.shortName, entry.id);
                }

                details += "\n\n~~ File Offsets ~~";
                details += string.Format("\nCPU: 0x{0:X}", entry.cpuOffsetDataHeader);
                details += string.Format("\nCPU length: {0} bytes (with padding)", entry.cpuRelativeOffsetNextEntry);
                if (entry.gpuDataLength > 0)
                {
                    details += string.Format(
                        "\nGPU: 0x{0:X}" +
                        "\nGPU length: {1} bytes",
                    entry.gpuOffsetData, entry.gpuDataLength);
                }

                if (entry.definitionId != 0 || entry.parentId != 0 || entry.daughterIds.Count > 0 || entry.instanceIds.Count > 0)
                {
                    details += "\n\n~~ Linked Node IDs ~~";
                }
                if (entry.definitionId != 0)
                {
                    details += string.Format("\nDefinition: {0:X8}", entry.definitionId);
                    linkPositions.Add(details.Length - 8);
                }
                if (entry.parentId != 0)
                {
                    details += string.Format("\nParent: {0:X8}", entry.parentId);
                    linkPositions.Add(details.Length - 8);
                }

                if (entry.daughterIds.Count == 1)
                {
                    details += string.Format("\nDaughter: {0:X8}", entry.daughterIds[0]);
                    linkPositions.Add(details.Length - 8);
                }
                if (entry.daughterIds.Count > 1)
                {
                    details += string.Format("\nDaughters:");
                    int count2 = entry.daughterIds.Count;
                    for (int j = 0; j < count2; j++)
                    {
                        details += string.Format("\n     {0:X8}", entry.daughterIds[j]);
                        linkPositions.Add(details.Length - 8);
                    }
                }

                if (entry.instanceIds.Count == 1)
                {
                    details += string.Format("\nInstance: {0:X8}", entry.instanceIds[0]);
                    linkPositions.Add(details.Length - 8);
                }
                if (entry.instanceIds.Count > 1)
                {
                    details += string.Format("\nInstances:");
                    int count2 = entry.instanceIds.Count;
                    for (int j = 0; j < count2; j++)
                    {
                        details += string.Format("\n     {0:X8}", entry.instanceIds[j]);
                        linkPositions.Add(details.Length - 8);
                    }
                }

                if (i < count - 1)
                {
                    details += "\n\n";
                }
            }
            if (listView1.SelectedItems.Count > 100)
            {
                details += "More than 100 items selected, the remaining details have been omitted.";
            }
            if (listView1.SelectedItems.Count == 0)
            {
                details += "Please select a data entry.";
            }
            richTextBox1.Clear();
            richTextBox1.Text = details;
            
            count = linkPositions.Count;
            for (int i = 0; i < count; i++)
            {
                int position = linkPositions[i];
                richTextBox1.SelectionStart = position;
                richTextBox1.Select(position, 8);
                richTextBox1.SetSelectionLink(true);
                richTextBox1.Select(position + 8, 0);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog fileDialog = new OpenFileDialog() { Filter = "CPU Files|*.cpu.***", Multiselect = false, Title = "Select a CPU file", InitialDirectory = lastDirectoryPath})
            {
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    cpuFile = new CpuFile(fileDialog.FileName);
                    toolStripStatusLabel1.Text = string.Format("Current file: {0} (Sumo tool v{2}, {1}-endian)", fileDialog.FileName, cpuFile.isLittleEndian ? "Little" : "Big", cpuFile.entriesList[0].toolVersion);
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
                CpuEntry entry = cpuFile.entriesList[int.Parse(listView1.SelectedItems[0].Text) - 1];
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

        private void richTextBox1_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            SelectEntryByID(e.LinkText);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Enter an ID";
                dialog.button1.Text = "Search";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SelectEntryByID(dialog.textBox1.Text);
                }
            }
        }

        private void SelectEntryByID(string idString)
        {
            if (idString.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase))
            {
                idString = idString.Substring(2);
            }
            if (uint.TryParse(idString, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out uint id))
            {
                int index = cpuFile.entriesList.FindIndex(i => i.id == id);
                if (index == -1)
                {
                    MessageBox.Show("Entry not found!\nIt may exist in a different CPU file.", "Entry not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    listView1.SelectedItems.Clear();
                    listView1.Items[index].Selected = true;
                    listView1.Select();
                }
            }
            else
            {
                MessageBox.Show("Invalid ID!\nYou must enter a 32bit hex number.", "Invalid ID!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
