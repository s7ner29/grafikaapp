namespace kursovaya
{
    partial class SortForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            lblTables = new Label();
            clbTables = new CheckedListBox();
            btnLoadRelations = new Button();
            lblRelations = new Label();
            clbRelations = new CheckedListBox();
            lblBaseTable = new Label();
            cbBaseTable = new ComboBox();
            btnLoadColumns = new Button();
            clbColumns = new CheckedListBox();
            lblSort = new Label();
            cbSortColumn = new ComboBox();
            cbSortDir = new ComboBox();
            btnOk = new Button();
            btnCancel = new Button();
            SuspendLayout();
            // 
            // lblTables
            // 
            lblTables.AutoSize = true;
            lblTables.Location = new Point(12, 12);
            lblTables.Name = "lblTables";
            lblTables.Size = new Size(111, 15);
            lblTables.TabIndex = 0;
            lblTables.Text = "выберите таблицы";
            lblTables.Click += lblTables_Click;
            // 
            // clbTables
            // 
            clbTables.CheckOnClick = true;
            clbTables.FormattingEnabled = true;
            clbTables.Location = new Point(12, 36);
            clbTables.Name = "clbTables";
            clbTables.Size = new Size(300, 184);
            clbTables.TabIndex = 1;
            clbTables.SelectedIndexChanged += clbTables_SelectedIndexChanged;
            // 
            // btnLoadRelations
            // 
            btnLoadRelations.Location = new Point(320, 46);
            btnLoadRelations.Name = "btnLoadRelations";
            btnLoadRelations.Size = new Size(140, 28);
            btnLoadRelations.TabIndex = 2;
            btnLoadRelations.Text = "найти связи";
            btnLoadRelations.UseVisualStyleBackColor = true;
            btnLoadRelations.Click += btnLoadRelations_Click;
            // 
            // lblRelations
            // 
            lblRelations.AutoSize = true;
            lblRelations.Location = new Point(320, 72);
            lblRelations.Name = "lblRelations";
            lblRelations.Size = new Size(101, 15);
            lblRelations.TabIndex = 3;
            lblRelations.Text = "найденные связи";
            lblRelations.Click += lblRelations_Click;
            // 
            // clbRelations
            // 
            clbRelations.CheckOnClick = true;
            clbRelations.FormattingEnabled = true;
            clbRelations.Location = new Point(320, 92);
            clbRelations.Name = "clbRelations";
            clbRelations.Size = new Size(300, 130);
            clbRelations.TabIndex = 4;
            clbRelations.SelectedIndexChanged += clbRelations_SelectedIndexChanged;
            // 
            // lblBaseTable
            // 
            lblBaseTable.AutoSize = true;
            lblBaseTable.Location = new Point(320, 244);
            lblBaseTable.Name = "lblBaseTable";
            lblBaseTable.Size = new Size(98, 15);
            lblBaseTable.TabIndex = 5;
            lblBaseTable.Text = "Базовая таблица";
            lblBaseTable.Click += lblBaseTable_Click;
            // 
            // cbBaseTable
            // 
            cbBaseTable.DropDownStyle = ComboBoxStyle.DropDownList;
            cbBaseTable.FormattingEnabled = true;
            cbBaseTable.Location = new Point(320, 264);
            cbBaseTable.Name = "cbBaseTable";
            cbBaseTable.Size = new Size(300, 23);
            cbBaseTable.TabIndex = 6;
            cbBaseTable.SelectedIndexChanged += cbBaseTable_SelectedIndexChanged;
            // 
            // btnLoadColumns
            // 
            btnLoadColumns.Location = new Point(12, 244);
            btnLoadColumns.Name = "btnLoadColumns";
            btnLoadColumns.Size = new Size(140, 28);
            btnLoadColumns.TabIndex = 7;
            btnLoadColumns.Text = "Загрузить колонки";
            btnLoadColumns.UseVisualStyleBackColor = true;
            btnLoadColumns.Click += btnLoadColumns_Click;
            // 
            // clbColumns
            // 
            clbColumns.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            clbColumns.CheckOnClick = true;
            clbColumns.FormattingEnabled = true;
            clbColumns.Location = new Point(12, 293);
            clbColumns.Name = "clbColumns";
            clbColumns.Size = new Size(608, 130);
            clbColumns.TabIndex = 8;
            clbColumns.SelectedIndexChanged += clbColumns_SelectedIndexChanged;
            // 
            // lblSort
            // 
            lblSort.AutoSize = true;
            lblSort.Location = new Point(12, 428);
            lblSort.Name = "lblSort";
            lblSort.Size = new Size(95, 15);
            lblSort.TabIndex = 9;
            lblSort.Text = "Сортировать по";
            lblSort.Click += lblSort_Click;
            // 
            // cbSortColumn
            // 
            cbSortColumn.DropDownStyle = ComboBoxStyle.DropDownList;
            cbSortColumn.FormattingEnabled = true;
            cbSortColumn.Location = new Point(12, 448);
            cbSortColumn.Name = "cbSortColumn";
            cbSortColumn.Size = new Size(420, 23);
            cbSortColumn.TabIndex = 10;
            cbSortColumn.SelectedIndexChanged += cbSortColumn_SelectedIndexChanged;
            // 
            // cbSortDir
            // 
            cbSortDir.DropDownStyle = ComboBoxStyle.DropDownList;
            cbSortDir.FormattingEnabled = true;
            cbSortDir.Items.AddRange(new object[] { "ASC, DESC" });
            cbSortDir.Location = new Point(440, 448);
            cbSortDir.Name = "cbSortDir";
            cbSortDir.Size = new Size(180, 23);
            cbSortDir.TabIndex = 0;
            cbSortDir.SelectedIndexChanged += cbSortDir_SelectedIndexChanged;
            // 
            // btnOk
            // 
            btnOk.Location = new Point(374, -1);
            btnOk.Name = "btnOk";
            btnOk.Size = new Size(120, 28);
            btnOk.TabIndex = 11;
            btnOk.Text = "Применить";
            btnOk.UseVisualStyleBackColor = true;
            btnOk.Click += btnOk_Click;
            // 
            // btnCancel
            // 
            btnCancel.Location = new Point(500, -1);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(120, 28);
            btnCancel.TabIndex = 12;
            btnCancel.Text = "Отмена";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // SortForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(624, 481);
            Controls.Add(btnCancel);
            Controls.Add(btnOk);
            Controls.Add(cbSortDir);
            Controls.Add(cbSortColumn);
            Controls.Add(lblSort);
            Controls.Add(clbColumns);
            Controls.Add(btnLoadColumns);
            Controls.Add(cbBaseTable);
            Controls.Add(lblBaseTable);
            Controls.Add(clbRelations);
            Controls.Add(lblRelations);
            Controls.Add(btnLoadRelations);
            Controls.Add(clbTables);
            Controls.Add(lblTables);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "SortForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "SortForm";
            Load += SortForm_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblTables;
        private CheckedListBox clbTables;
        private Button btnLoadRelations;
        private Label lblRelations;
        private CheckedListBox clbRelations;
        private Label lblBaseTable;
        private ComboBox cbBaseTable;
        private Button btnLoadColumns;
        private CheckedListBox clbColumns;
        private Label lblSort;
        private ComboBox cbSortColumn;
        private ComboBox cbSortDir;
        private Button btnOk;
        private Button btnCancel;
    }
}