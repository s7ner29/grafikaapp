using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace kursovaya
{
    // Дополнительный partial?файл с обработчиками событий,
    // на которые ссылается Form1.Designer.cs.
    public partial class Form1
    {
        // ---------------------- Утилиты ----------------------

        // Экспортирует именно то, что видно в DataGridView (порядок/фильтрация/видимые строки)
        private void ExportGridToCsv(DataGridView? dgv, string defaultFileName)
        {
            if (dgv == null) return;

            try
            {
                using var sfd = new SaveFileDialog
                {
                    FileName = defaultFileName,
                    Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    Title = "Сохранить в CSV"
                };

                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                using var sw = new StreamWriter(sfd.FileName, false, Encoding.UTF8);

                // Заголовки в порядке колонок DataGridView (видимые)
                var visibleCols = dgv.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToArray();
                sw.WriteLine(string.Join(";", visibleCols.Select(c => QuoteCsv(c.HeaderText ?? c.Name))));

                // Строки в порядке отображения (учитывают фильтрацию/сортировку)
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.IsNewRow) continue;
                    if (!row.Visible) continue;

                    var parts = visibleCols.Select(c =>
                    {
                        var val = row.Cells[c.Index].Value;
                        return QuoteCsv(val == null || val == DBNull.Value ? string.Empty : Convert.ToString(val));
                    });

                    sw.WriteLine(string.Join(";", parts));
                }

                MessageBox.Show(this, "Экспорт завершён.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при экспорте в CSV:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            static string QuoteCsv(string? v) => $"\"{(v ?? string.Empty).Replace("\"", "\"\"")}\"";
        }

        // Открывает форму сортировки для указанного DataGridView
        private void OpenSortDialogForGrid(DataGridView? dgv)
        {
            if (dgv == null) return;
            try
            {
                using var dlg = new SortForm(_dbPath, dgv);
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка открытия окна сортировки:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------------------- Поиск / фильтрация ----------------------

        // Фильтрует содержимое DataGridView по строке поиска.
        // Для DataTable использует RowFilter (эффективно и сохраняет источник данных),
        // в противном случае скрывает строки, не соответствующие запросу.
        private void FilterGridByText(DataGridView? dgv, string? rawQuery)
        {
            if (dgv == null) return;

            var query = (rawQuery ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(query))
            {
                // Очистка фильтра / восстановление всех строк
                if (dgv.DataSource is DataTable dt)
                {
                    try { dt.DefaultView.RowFilter = string.Empty; }
                    catch { /* ignore */ }
                }
                else
                {
                    foreach (DataGridViewRow r in dgv.Rows) r.Visible = true;
                }
                dgv.ClearSelection();
                try { if (dgv.CurrentCell != null) dgv.CurrentCell = null; } catch { }
                return;
            }

            // Безопасное значение для LIKE в выражении RowFilter
            var s = query.Replace("'", "''");

            if (dgv.DataSource is DataTable table)
            {
                try
                {
                    // выбираем текстовые колонки
                    var textCols = table.Columns.Cast<DataColumn>().Where(c => c.DataType == typeof(string)).Select(c => c.ColumnName).ToArray();

                    string[] parts;
                    if (textCols.Length > 0)
                    {
                        parts = textCols.Select(c => $"[{EscapeColumnName(c)}] LIKE '%{s}%'").ToArray();
                    }
                    else
                    {
                        // если нет строковых колонок — применять CONVERT ко всем колонкам
                        var allCols = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
                        parts = allCols.Select(c => $"CONVERT([{EscapeColumnName(c)}], 'System.String') LIKE '%{s}%'").ToArray();
                    }

                    var filter = string.Join(" OR ", parts);
                    table.DefaultView.RowFilter = filter;
                    dgv.ClearSelection();
                }
                catch (Exception)
                {
                    // при ошибке безопасно откатиться к построчной фильтрации
                    foreach (DataGridViewRow r in dgv.Rows)
                    {
                        bool matched = false;
                        foreach (DataGridViewCell cell in r.Cells)
                        {
                            try
                            {
                                var v = cell.Value;
                                if (v != null && v != DBNull.Value)
                                {
                                    if (Convert.ToString(v)!.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) { matched = true; break; }
                                }
                            }
                            catch { /* ignore cell */ }
                        }
                        r.Visible = matched;
                    }
                }
            }
            else
            {
                // Фильтруем вручную по видимым колонкам/ячейкам
                foreach (DataGridViewRow r in dgv.Rows)
                {
                    if (r.IsNewRow) { r.Visible = false; continue; }
                    bool matched = false;
                    foreach (DataGridViewCell cell in r.Cells)
                    {
                        try
                        {
                            var v = cell.Value;
                            if (v == null || v == DBNull.Value) continue;
                            if (Convert.ToString(v)!.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) { matched = true; break; }
                        }
                        catch { /* ignore cell */ }
                    }
                    r.Visible = matched;
                }
            }

            dgv.ClearSelection();
            try { if (dgv.CurrentCell != null) dgv.CurrentCell = null; } catch { }
        }

        private static string EscapeColumnName(string name)
        {
            // В DataView RowFilter имена колонок в квадратных скобках; удваиваем закрывающую скобку внутри имени
            return name?.Replace("]", "]]") ?? name;
        }

        // ---------------------- TabPage1 (ученики) ----------------------

        private void button10_Click(object? sender, EventArgs e) { } // подключиться (оставлено пустым)

        private void button9_Click(object? sender, EventArgs e) { } // отключиться (оставлено пустым)

        // Поиск — ученики (кнопка)
        private void button8_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView1") as DataGridView;
            FilterGridByText(dgv, textBox1?.Text);
        }

        // Поиск по вводу — можно выполнять и на ввод (необязательно)
        private void textBox1_TextChanged(object? sender, EventArgs e)
        {
            // не активируем автопоиск — пользователь предпочёл кнопку "поиск".
            // Если нужно — раскомментируйте следующую строку:
            // FilterGridByText(FindControlRecursive(this, "dataGridView1") as DataGridView, textBox1?.Text);
        }

        // Обновить — загружает исходную таблицу "ученики", очищая текущее содержимое грида
        private void button7_Click(object? sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView1") as DataGridView;
                if (ctrl != null)
                {
                    ctrl.DataSource = null;
                    ctrl.Columns.Clear();
                }

                LoadStudentsToGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка обновления списка учеников:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Сортировать (ученики)
        private void button5_Click(object? sender, EventArgs e)
        {
            var ctrl = FindControlRecursive(this, "dataGridView1") as DataGridView;
            OpenSortDialogForGrid(ctrl);
        }

        // Сохранить в CSV (ученики) — сохраняем именно текущее отображение грида
        private void button6_Click(object? sender, EventArgs e)
        {
            var ctrl = FindControlRecursive(this, "dataGridView1") as DataGridView;
            if (ctrl != null) ExportGridToCsv(ctrl, "students.csv");
        }

        // Удаление ученика — вызывает процедуру удаления каскадом через DbProcedures
        private void button4_Click(object? sender, EventArgs e)
        {
            try
            {
                var dgv = FindControlRecursive(this, "dataGridView1") as DataGridView;
                if (dgv == null) return;

                var id = GetSelectedIdFromGrid(dgv, "STUDENT_ID");
                if (id == null)
                {
                    var dt = global::kursovaya.FirebirdDb.ExecuteQuery(_dbPath,
                        "SELECT STUDENT_ID, TRIM(LASTNAME || ' ' || FIRSTNAME || ' ' || COALESCE(MIDDLENAME,'')) AS FULLNAME FROM STUDENTS ORDER BY LASTNAME, FIRSTNAME");
                    id = ShowPickIdDialog("Выберите ученика для удаления", dt, "STUDENT_ID", "FULLNAME");
                }

                if (id == null) return;

                if (MessageBox.Show(this, "Вы уверены, что хотите удалить ученика (включая связанные записи)?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                DbProcedures.DeleteStudentCascade(_dbPath, id.Value);
                LoadStudentsToGrid();
                MessageBox.Show(this, "Ученик удалён.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка удаления ученика:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dataGridView1_CellContentClick(object? sender, DataGridViewCellEventArgs e) { }

        // ---------------------- TabPage2 (учителя) ----------------------

        private void button20_Click(object? sender, EventArgs e) { } // подключиться

        private void button19_Click(object? sender, EventArgs e) { } // отключиться

        // Поиск — учителя (кнопка)
        private void button18_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView2") as DataGridView;
            FilterGridByText(dgv, textBox2?.Text);
        }

        private void textBox2_TextChanged(object? sender, EventArgs e)
        {
            // автопоиск отключён по умолчанию
        }

        private void button17_Click(object? sender, EventArgs e) // сохранить в CSV (учителя)
        {
            var ctrl = FindControlRecursive(this, "dataGridView2") as DataGridView;
            if (ctrl != null) ExportGridToCsv(ctrl, "teachers.csv");
        }

        // Обновить — загружает исходную таблицу "учителя"
        private void button16_Click(object? sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView2") as DataGridView;
                if (ctrl != null)
                {
                    ctrl.DataSource = null;
                    ctrl.Columns.Clear();
                }

                LoadTeachersToGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка обновления списка учителей:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button15_Click(object? sender, EventArgs e) // сортировать (учителя)
        {
            var ctrl = FindControlRecursive(this, "dataGridView2") as DataGridView;
            OpenSortDialogForGrid(ctrl);
        }

        // Удаление учителя — вызывает процедуру удаления каскадом через DbProcedures
        private void button14_Click(object? sender, EventArgs e)
        {
            try
            {
                var dgv = FindControlRecursive(this, "dataGridView2") as DataGridView;
                if (dgv == null) return;

                var id = GetSelectedIdFromGrid(dgv, "TEACHER_ID");
                if (id == null)
                {
                    var dt = global::kursovaya.FirebirdDb.ExecuteQuery(_dbPath,
                        "SELECT TEACHER_ID, TRIM(LASTNAME || ' ' || FIRSTNAME || ' ' || COALESCE(MIDDLENAME,'')) AS FULLNAME FROM TEACHERS ORDER BY LASTNAME, FIRSTNAME");
                    id = ShowPickIdDialog("Выберите учителя для удаления", dt, "TEACHER_ID", "FULLNAME");
                }

                if (id == null) return;

                if (MessageBox.Show(this, "Вы уверены, что хотите удалить учителя (включая связанные записи)?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                DbProcedures.DeleteTeacherCascade(_dbPath, id.Value);
                LoadTeachersToGrid();
                MessageBox.Show(this, "Учитель удалён.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка удаления учителя:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button12_Click(object? sender, EventArgs e) { } // изменить

        // Добавить учителя — открывает диалог AddTeacherDialog
        private void button11_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dlg = new AddTeacherDialog(_dbPath);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    LoadTeachersToGrid();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка добавления учителя:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dataGridView2_CellContentClick(object? sender, DataGridViewCellEventArgs e) { }

        // ---------------------- TabPage3 (журнал) ----------------------

        private void button30_Click(object? sender, EventArgs e) { } // подключиться

        private void button29_Click(object? sender, EventArgs e) { } // отключиться

        // Поиск — журнал (кнопка)
        private void button28_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView3") as DataGridView;
            FilterGridByText(dgv, textBox3?.Text);
        }

        private void textBox3_TextChanged(object? sender, EventArgs e) { }

        private void button27_Click(object? sender, EventArgs e) // сохранить в CSV (журнал)
        {
            var ctrl = FindControlRecursive(this, "dataGridView3") as DataGridView;
            if (ctrl != null) ExportGridToCsv(ctrl, "journal.csv");
        }

        // Обновить — загружает исходный журнал (GET_FULL_JOURNAL)
        private void button26_Click(object? sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView3") as DataGridView;
                if (ctrl != null)
                {
                    ctrl.DataSource = null;
                    ctrl.Columns.Clear();
                }

                LoadFullJournalToGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка обновления журнала:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button25_Click(object? sender, EventArgs e) // сортировать (журнал)
        {
            var ctrl = FindControlRecursive(this, "dataGridView3") as DataGridView;
            OpenSortDialogForGrid(ctrl);
        }

        private void button24_Click(object? sender, EventArgs e) { } // сохранить

        private void button23_Click(object? sender, EventArgs e) { } // изменить

        // Добавить оценку — открывает AddGradeDialog и обновляет журнал
        private void button21_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dlg = new AddGradeDialog(_dbPath);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    LoadFullJournalToGrid();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка добавления оценки:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void dataGridView3_CellContentClick(object? sender, DataGridViewCellEventArgs e) { }

        // ---------------------- TabPage4 (учебный план) ----------------------

        private void button40_Click(object? sender, EventArgs e) { } // подключиться

        private void button39_Click(object? sender, EventArgs e) { } // отключиться

        // Поиск — учебный план (кнопка)
        private void button38_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView4") as DataGridView;
            FilterGridByText(dgv, textBox4?.Text);
        }

        private void textBox4_TextChanged(object? sender, EventArgs e) { }

        private void button36_Click(object? sender, EventArgs e) // сохранить в CSV (учебный план)
        {
            var ctrl = FindControlRecursive(this, "dataGridView4") as DataGridView;
            if (ctrl != null) ExportGridToCsv(ctrl, "curriculum.csv");
        }

        // Обновить — загружает исходный учебный план (GET_CURRICULUM)
        private void button37_Click(object? sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView4") as DataGridView;
                if (ctrl != null)
                {
                    ctrl.DataSource = null;
                    ctrl.Columns.Clear();
                }

                LoadCurriculumToGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка обновления учебного плана:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button35_Click(object? sender, EventArgs e) // сортировать (учебный план)
        {
            var ctrl = FindControlRecursive(this, "dataGridView4") as DataGridView;
            OpenSortDialogForGrid(ctrl);
        }

        private void dataGridView4_CellContentClick(object? sender, DataGridViewCellEventArgs e) { }

        // ---------------------- TabPage5 (достижения) ----------------------

        private void button50_Click(object? sender, EventArgs e) { } // подключиться

        private void button49_Click(object? sender, EventArgs e) { } // отключиться

        // Поиск — достижения (кнопка)
        private void button48_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView5") as DataGridView;
            FilterGridByText(dgv, textBox5?.Text);
        }

        private void textBox5_TextChanged(object? sender, EventArgs e) { }

        private void button47_Click(object? sender, EventArgs e) // сохранить в CSV (достижения)
        {
            var ctrl = FindControlRecursive(this, "dataGridView5") as DataGridView;
            if (ctrl != null) ExportGridToCsv(ctrl, "achievements.csv");
        }

        // Обновить — загружает детали достижений (GET_STUDENT_ACHIEVEMENTS_DETAIL)
        private void button46_Click(object? sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView5") as DataGridView;
                if (ctrl != null)
                {
                    ctrl.DataSource = null;
                    ctrl.Columns.Clear();
                }

                LoadAchievementsToGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка обновления достижений:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button45_Click(object? sender, EventArgs e) // сортировать (достижения)
        {
            var ctrl = FindControlRecursive(this, "dataGridView5") as DataGridView;
            OpenSortDialogForGrid(ctrl);
        }

        private void button42_Click(object? sender, EventArgs e) { } // изменить

        // Добавить достижение
        private void button41_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dlg = new AddAchievementDialog(_dbPath);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    LoadAchievementsToGrid();
                    LoadFullJournalToGrid(); // в случае, если достижения влияют на журнал/отображение
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка добавления достижения:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void pictureBox3_Click(object? sender, EventArgs e) { }

        private void dataGridView5_CellContentClick(object? sender, DataGridViewCellEventArgs e) { }

        // ---------------------- TabPage6 (классы) ----------------------

        private void button60_Click(object? sender, EventArgs e) { } // подключиться

        private void button59_Click(object? sender, EventArgs e) { } // отключиться

        // Поиск — классы (кнопка)
        private void button58_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView6") as DataGridView;
            FilterGridByText(dgv, textBox6?.Text);
        }

        private void textBox6_TextChanged(object? sender, EventArgs e) { }

        private void button57_Click(object? sender, EventArgs e) // сохранить в CSV (классы)
        {
            var ctrl = FindControlRecursive(this, "dataGridView6") as DataGridView;
            if (ctrl != null) ExportGridToCsv(ctrl, "classes.csv");
        }

        // Обновить — загружает детали классов (GET_CLASS_DETAILS)
        private void button56_Click(object? sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView6") as DataGridView;
                if (ctrl != null)
                {
                    ctrl.DataSource = null;
                    ctrl.Columns.Clear();
                }

                LoadClassDetailsToGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка обновления классов:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Добавить класс — простой диалог ввода имени и опционального классного руководителя
        private void button55_Click(object? sender, EventArgs e)
        {
            try
            {
                using var dlg = new Form
                {
                    Text = "Добавить класс",
                    StartPosition = FormStartPosition.CenterParent,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(8)
                };

                var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

                var tbName = new TextBox { Dock = DockStyle.Fill };
                var cbTeacher = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };

                // Добавляем строки в таблицу
                void AddRow(string label, Control ctrl)
                {
                    var idx = tbl.RowCount++;
                    tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    tbl.Controls.Add(new Label { Text = label, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft }, 0, idx);
                    ctrl.Dock = DockStyle.Fill;
                    tbl.Controls.Add(ctrl, 1, idx);
                }

                AddRow("Имя класса", tbName);
                AddRow("Классный руководитель", cbTeacher);

                // Заполнить список учителей (первый элемент — без назначенного руководителя)
                cbTeacher.Items.Add(new Tuple<int?, string>(null, "(Не назначать)"));
                try
                {
                    var dt = global::kursovaya.FirebirdDb.ExecuteQuery(_dbPath,
                        "SELECT TEACHER_ID, TRIM(LASTNAME || ' ' || FIRSTNAME || ' ' || COALESCE(MIDDLENAME,'')) AS FULLNAME FROM TEACHERS ORDER BY LASTNAME, FIRSTNAME");
                    foreach (DataRow r in dt.Rows)
                    {
                        var id = Convert.ToInt32(r["TEACHER_ID"]);
                        var name = Convert.ToString(r["FULLNAME"]) ?? string.Empty;
                        cbTeacher.Items.Add(new Tuple<int?, string>(id, name));
                    }
                }
                catch
                {
                    // если не удалось загрузить учителей — оставляем только вариант "Не назначать"
                }
                if (cbTeacher.Items.Count > 0) cbTeacher.SelectedIndex = 0;
                cbTeacher.DisplayMember = "Item2";
                cbTeacher.ValueMember = "Item1";

                var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 100 };
                var btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 100 };
                var pnl = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
                pnl.Controls.Add(btnOk); pnl.Controls.Add(btnCancel);

                dlg.Controls.Add(tbl);
                dlg.Controls.Add(pnl);

                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                var className = tbName.Text?.Trim();
                if (string.IsNullOrWhiteSpace(className))
                {
                    MessageBox.Show(this, "Имя класса не указано.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int? teacherId = null;
                if (cbTeacher.SelectedItem is Tuple<int?, string> picked)
                    teacherId = picked.Item1;

                var newId = DbProcedures.AddClass(_dbPath, className, teacherId);
                MessageBox.Show(this, $"Класс добавлен (ID={newId}).", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadClassDetailsToGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка добавления класса:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // кнопка сохранения классов реализована в Form1.cs (button53_Click)
       

        private void button52_Click(object? sender, EventArgs e) // сортировать (классы)
        {
            var ctrl = FindControlRecursive(this, "dataGridView6") as DataGridView;
            OpenSortDialogForGrid(ctrl);
        }

        private void dataGridView6_CellContentClick(object? sender, DataGridViewCellEventArgs e) { }

        private void button32_Click(object? sender, EventArgs e) { }

        private void button54_Click(object? sender, EventArgs e) { }
    }
}