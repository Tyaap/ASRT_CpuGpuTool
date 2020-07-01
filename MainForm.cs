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

        private void RefreshCpuEntryList(bool keepSelectedItems = false)
        {
            int[] selectedIndices = null;
            int focusedItemIndex = 0;
            if (keepSelectedItems)
            {
                focusedItemIndex = listView1.FocusedItem.Index;
                selectedIndices = new int[listView1.SelectedIndices.Count];
                listView1.SelectedIndices.CopyTo(selectedIndices, 0);
            }

            listView1.Items.Clear();
            int count = cpuFile.Count;
            List<ListViewItem> items = new List<ListViewItem>();
            for (int i = 0; i < count; i++)
            {
                CpuEntry entry = cpuFile[i];
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

            if (keepSelectedItems)
            {
                count = selectedIndices.Length;
                for (int i = 0; i < count; i++)
                {
                    listView1.Items[selectedIndices[i]].Selected = true;
                }
                listView1.Items[focusedItemIndex].Focused = true;
                listView1.Items[focusedItemIndex].EnsureVisible();
            }
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
                CpuEntry entry = cpuFile[int.Parse(listView1.SelectedItems[i].Text) - 1];
                Node node = entry as Node;
                Resource resource = entry as Resource;
                ListViewItem item = listView1.SelectedItems[i];

                details += string.Format("------------ ENTRY #{0} ------------", entry.entryNumber);
                details += (node != null) ? "\nSumo Engine Node" : "\nSumo Loader Resource";
                details += string.Format("\nType: {0} ({0:X})", entry.dataType);
                details += string.Format("\nEntry ID: {0:X8}", entry.id);
                details += string.Format("\nName: {0}", entry.name, entry.id);
                if (node != null && !string.IsNullOrEmpty(node.shortName))
                {
                    details += string.Format("\nShort name: {0}", node.shortName, entry.id);
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

                if (node != null)
                {
                    if (node.definitionId != 0 || node.parentId != 0 || node.daughterIds.Count > 0 || node.instanceIds.Count > 0)
                    {
                        details += "\n\n~~ Linked Entries ~~";
                    }
                    if (node.definitionId != 0)
                    {
                        details += string.Format("\nDefinition node: {0:X8}", node.definitionId);
                        linkPositions.Add(details.Length - 8);
                    }
                    if (node.parentId != 0)
                    {
                        details += string.Format("\nParent node: {0:X8}", node.parentId);
                        linkPositions.Add(details.Length - 8);
                    }
                    if (node.daughterIds.Count == 1)
                    {
                        details += string.Format("\nDaughter node: {0:X8}", node.daughterIds[0]);
                        linkPositions.Add(details.Length - 8);
                    }
                    if (node.daughterIds.Count > 1)
                    {
                        details += string.Format("\nDaughter nodes:");
                        int count2 = node.daughterIds.Count;
                        for (int j = 0; j < count2; j++)
                        {
                            details += string.Format("\n     {0:X8}", node.daughterIds[j]);
                            linkPositions.Add(details.Length - 8);
                        }
                    }
                    if (node.instanceIds.Count == 1)
                    {
                        details += string.Format("\nInstance node: {0:X8}", node.instanceIds[0]);
                        linkPositions.Add(details.Length - 8);
                    }
                    if (node.instanceIds.Count > 1)
                    {
                        details += string.Format("\nInstance nodes:");
                        int count2 = node.instanceIds.Count;
                        for (int j = 0; j < count2; j++)
                        {
                            details += string.Format("\n     {0:X8}", node.instanceIds[j]);
                            linkPositions.Add(details.Length - 8);
                        }
                    }
                    if (node.partenerResourceId != 0)
                    {
                        details += string.Format("\nPartener resource: {0:X8}", node.partenerResourceId);
                        linkPositions.Add(details.Length - 8);
                    }
                }

                if (resource != null)
                {
                    if (resource.partenerNodeId != 0)
                    {
                        details += "\n\n~~ Linked Entries ~~";
                    }
                    if (resource.partenerNodeId != 0)
                    {
                        details += string.Format("\nPartener node: {0:X8}", resource.partenerNodeId);
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
                details += "Please select an entry.";
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
                    toolStripStatusLabel1.Text = string.Format("Current file: {0} (Sumo tool v{2}, {1}-endian)", fileDialog.FileName, cpuFile.isLittleEndian ? "Little" : "Big", cpuFile[0].toolVersion);
                    RefreshCpuEntryList();
                    RefreshEntryStatus();
                    RefreshDetailsText();
                    RefreshDataTypeChoices();
                    lastDirectoryPath = Path.GetDirectoryName(fileDialog.FileName);
                    button2.Enabled = button4.Enabled = textBox1.Enabled = comboBox1.Enabled = true;
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

        private void SaveToFile(bool cpuData, bool gpuData)
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
                        if (cpuData)
                        {
                            fileNames.Add(cpuFile.SaveCpuData(entryIndex, folderBrowserDialog.SelectedPath));
                        }
                        if (gpuData)
                        {
                            fileName = cpuFile.SaveGpuData(entryIndex, folderBrowserDialog.SelectedPath);
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                fileNames.Add(fileName);
                            }
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

        private void richTextBox1_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            SelectEntryByID(e.LinkText, EntryType.Node);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Enter a node ID";
                dialog.button1.Text = "Search";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SelectEntryByID(dialog.textBox1.Text, EntryType.Node);
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Enter a resource ID";
                dialog.button1.Text = "Search";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    SelectEntryByID(dialog.textBox1.Text, EntryType.Resource);
                }
            }
        }

        private bool ParseHexString(string idString, out uint id)
        {
            if (idString.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase))
            {
                idString = idString.Substring(2);
            }
            return uint.TryParse(idString, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out id);
        }

        private void SelectEntryByID(string idString, EntryType type)
        {
            if (!ParseHexString(idString, out uint id))
            {
                MessageBox.Show("Invalid ID!\nYou must enter a 32bit hex number.", "Invalid ID!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            CpuEntry entry;
            if (type == EntryType.Node)
            {
                if (!cpuFile.nodeDictionary.TryGetValue(id, out Node node))
                {
                    MessageBox.Show("Node not found!\nIt may exist in a different CPU file.",
                    "Not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                entry = node;
            }
            else
            {
                if (!cpuFile.resourceDictionary.TryGetValue(id, out Resource resource))
                {
                    MessageBox.Show("Resource not found!\nIt may exist in a different CPU file.",
                        "Not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                        
                }
                entry = resource;
            }

            // Clear other searches
            typeFilter = "";
            nameFilter = "";
            RefreshCpuEntryList();
            RefreshEntryStatus();

            var item = listView1.Items[entry.entryNumber - 1];
            int index = entry.entryNumber - 1;
            listView1.SelectedItems.Clear();
            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            listView1.Select();
        }

        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (listView1.FocusedItem.Bounds.Contains(e.Location))
                {
                    int count = listView1.SelectedItems.Count;
                    bool gpuDataAvailable = false;
                    bool allNodesHaveDefinitions = true;
                    bool allNodesHaveParents = true;
                    for (int i = 0; i < count; i++)
                    {
                        int entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text) - 1;
                        if (!gpuDataAvailable && cpuFile[entryIndex].gpuDataLength > 0)
                        {
                            gpuDataAvailable = true;
                        }

                        Node node = cpuFile[entryIndex] as Node;
                        if (allNodesHaveDefinitions && (node == null || node.definitionId == 0))
                        {
                            allNodesHaveDefinitions = false;
                        }
                        if (allNodesHaveParents && (node == null || node.parentId == 0))
                        {
                            allNodesHaveParents = false;
                        }
                        if (gpuDataAvailable && !allNodesHaveDefinitions && !allNodesHaveParents)
                        {
                            break;
                        }
                    }

                    ((ToolStripMenuItem)contextMenuStrip1.Items["editToolStripMenuItem"]).DropDownItems["changeDefinitionToolStripMenuItem"].Enabled = allNodesHaveDefinitions;
                    ((ToolStripMenuItem)contextMenuStrip1.Items["editToolStripMenuItem"]).DropDownItems["changeParentToolStripMenuItem"].Enabled = allNodesHaveParents;

                    ((ToolStripMenuItem)contextMenuStrip1.Items["saveToFileToolStripMenuItem"]).DropDownItems["gpuDataToolStripMenuItem"].Enabled =
                        ((ToolStripMenuItem)contextMenuStrip1.Items["saveToFileToolStripMenuItem"]).DropDownItems["bothToolStripMenuItem"].Enabled = gpuDataAvailable;



                    contextMenuStrip1.Items["pasteToolStripMenuItem"].Enabled = (GetDataFromClipboard() != null);

                    contextMenuStrip1.Show(Cursor.Position);
                }
            }
        }

        public void PutDataOnClipboard(MemoryStream[] data)
        {
            DataObject clipboardData = new DataObject();
            clipboardData.SetData(typeof(MemoryStream[]), data);
            Clipboard.SetDataObject(clipboardData, true);
        }

        public MemoryStream[] GetDataFromClipboard()
        {
            DataObject retrievedData = Clipboard.GetDataObject() as DataObject;
            if (retrievedData == null || !retrievedData.GetDataPresent(typeof(MemoryStream[])))
                return null;
            return (MemoryStream[]) retrievedData.GetData(typeof(MemoryStream[]));
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MemoryStream cpuData = new MemoryStream();
            MemoryStream gpuData = new MemoryStream();

            int count = listView1.SelectedItems.Count;
            for (int i = 0; i < count; i++)
            {
                int entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text) - 1;
                cpuFile.GetCpuData(entryIndex, cpuData, -1);
                cpuFile.GetGpuData(entryIndex, gpuData, -1);
            }
            PutDataOnClipboard(new MemoryStream[] { cpuData, gpuData });
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int entryIndex = int.Parse(listView1.FocusedItem.SubItems[0].Text) - 1;
            MemoryStream[] data = GetDataFromClipboard();
            cpuFile.InsertCpuData(entryIndex, data[0]);
            cpuFile.InsertGpuData(entryIndex, data[1]);
            cpuFile.Reload();
            RefreshCpuEntryList();
            RefreshEntryStatus();
            RefreshDetailsText();
            RefreshDataTypeChoices();
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int count = listView1.SelectedItems.Count;
            for (int i = count - 1; i >= 0; i--) // Process in reverse order, to ensure offsets do not change while modifying
            {
                int entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text) - 1;
                cpuFile.DeleteCpuData(entryIndex);
                cpuFile.DeleteGpuData(entryIndex);
            }
            cpuFile.Reload();
            RefreshCpuEntryList();
            RefreshEntryStatus();
            RefreshDetailsText();
            RefreshDataTypeChoices();
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyToolStripMenuItem_Click(sender, e);
            DeleteToolStripMenuItem_Click(sender, e);
        }

        private void CpuDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveToFile(true, false);
        }

        private void GpuDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveToFile(false, true);
        }

        private void BothToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveToFile(true, true);
        }

        private void RenameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Choose a name";
                dialog.textBox1.Text = listView1.FocusedItem.SubItems[2].Text;
                dialog.button1.Text = "Confirm";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    int count = listView1.SelectedItems.Count;
                    for (int i = 0; i < count; i++)
                    {
                        int entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text) - 1;
                        cpuFile.ChangeEntryName(int.Parse(listView1.SelectedItems[i].Text) - 1, dialog.textBox1.Text);
                    }
                    cpuFile.Reload();
                    RefreshCpuEntryList(true);
                }
            }
        }

        private void EntryIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int entryIndex = int.Parse(listView1.FocusedItem.SubItems[0].Text) - 1;
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Choose an ID";
                dialog.textBox1.Text = cpuFile[entryIndex].id.ToString("X8");
                dialog.button1.Text = "Confirm";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (ParseHexString(dialog.textBox1.Text, out uint id))
                    {
                        int count = listView1.SelectedItems.Count;
                        for (int i = 0; i < count; i++)
                        {
                            entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text) - 1;
                            cpuFile.ChangeEntryID(entryIndex, id);
                        }
                        cpuFile.Reload();
                        RefreshDetailsText();
                    }
                }
            }
        }

        private void ChangeDefinitionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int entryIndex = int.Parse(listView1.FocusedItem.SubItems[0].Text) - 1;
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Choose a definition ID";
                dialog.textBox1.Text = ((Node)cpuFile[entryIndex]).definitionId.ToString("X8");
                dialog.button1.Text = "Confirm";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (ParseHexString(dialog.textBox1.Text, out uint id))
                    {
                        int count = listView1.SelectedItems.Count;
                        for (int i = 0; i < count; i++)
                        {
                            entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text) - 1;
                            cpuFile.ChangeDefinitionID(entryIndex, id);
                        }
                        cpuFile.Reload();
                        RefreshDetailsText();
                    }
                }
            }
        }

        private void ChangeParentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int entryIndex = int.Parse(listView1.FocusedItem.SubItems[0].Text) - 1;
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Choose a parent ID";
                dialog.textBox1.Text = ((Node)cpuFile[entryIndex]).parentId.ToString("X8");
                dialog.button1.Text = "Confirm";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (ParseHexString(dialog.textBox1.Text, out uint id))
                    {
                        int count = listView1.SelectedItems.Count;
                        for (int i = 0; i < count; i++)
                        {
                            entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text) - 1;
                            cpuFile.ChangeParentID(entryIndex, id);
                        }
                        cpuFile.Reload();
                        RefreshDetailsText();
                    }
                }
            }
        }
    }
}
