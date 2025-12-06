using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;

namespace kursovaya
{
    // Дополнительный partial?файл с обработчиками событий,
    // на которые ссылается Form1.Designer.cs.
    public partial class Form1
    {
        private bool _isConnected = false;

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

        // ---------------------- Вспомогательные для загрузки фото ----------------------

        // Загружает изображение из файла в независимый Bitmap (чтобы не блокировать файл)
        private Image? LoadImageFromFile(string filePath)
        {
            try
            {
                var bytes = File.ReadAllBytes(filePath);
                using var ms = new MemoryStream(bytes);
                using var img = Image.FromStream(ms);
                return new Bitmap(img); // копия независимая от потока
            }
            catch
            {
                return null;
            }
        }   

        // Открывает диалог выбора изображения и устанавливает его в PictureBox (освобождает предыдущий)
        private void SelectImageForPictureBox(PictureBox pb)
        {
            if (pb == null) return;

            using var ofd = new OpenFileDialog
            {
                Filter = "Изображения (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Все файлы (*.*)|*.*",
                Title = "Выберите изображение"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            var img = LoadImageFromFile(ofd.FileName);
            if (img == null)
            {
                MessageBox.Show(this, "Невозможно загрузить изображение.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Заменяем изображение, освобождая предыдущий ресурс
            try
            {
                var prev = pb.Image;
                pb.Image = img;
                prev?.Dispose();
            }
            catch
            {
                // на случай ошибок управления ресурсами — попытаться безопасно присвоить
                try
                {
                    pb.Image = img;
                }
                catch
                {
                    img.Dispose();
                    MessageBox.Show(this, "Не удалось установить изображение в элемент.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ---------------------- Поиск / фильтрация ----------------------

        // Показать простой диалог для выбора колонки поиска.
        // Возвращает internal column name (DataGridViewColumn.Name) или null = "все колонки".
        private string? ShowSearchColumnDialog(DataGridView dgv)
        {
            using var dlg = new Form
            {
                Text = "Выберите поле для поиска",
                StartPosition = FormStartPosition.CenterParent,
                Width = 420,
                Height = 140,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false
            };

            var cb = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            // Добавляем вариант "Все поля"
            cb.Items.Add(new Tuple<string?, string>(null, "(Все поля)"));

            // Используем видимые колонки грида (порядок DisplayIndex)
            foreach (DataGridViewColumn col in dgv.Columns.Cast<DataGridViewColumn>().OrderBy(c => c.DisplayIndex))
            {
                // отображаем HeaderText, сохраняем Name
                cb.Items.Add(new Tuple<string?, string>(col.Name, string.IsNullOrEmpty(col.HeaderText) ? col.Name : col.HeaderText));
            }

            cb.DisplayMember = "Item2";
            cb.ValueMember = "Item1";
            if (cb.Items.Count > 0) cb.SelectedIndex = 0;

            var pnl = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 100 };
            var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 100 };
            pnl.Controls.Add(ok); pnl.Controls.Add(cancel);

            dlg.Controls.Add(cb);
            dlg.Controls.Add(pnl);
            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;

            if (dlg.ShowDialog(this) != DialogResult.OK) return null;

            if (cb.SelectedItem is Tuple<string?, string> tup) return tup.Item1;
            return null;
        }

        // Фильтрует содержимое DataGridView по строке поиска.
        // Если указана columnName — фильтрация применяется только к этой колонке (по internal Name).
        // Для DataTable использует RowFilter (эффективно и сохраняет источник данных),
        // в противном случае скрывает строки, не соответствующие запросу.
        private void FilterGridByText(DataGridView? dgv, string? rawQuery, string? columnName = null)
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
                    if (!string.IsNullOrEmpty(columnName) && table.Columns.Contains(columnName))
                    {
                        var col = table.Columns[columnName];
                        if (col != null)
                        {
                            // Строковый столбец
                            if (col.DataType == typeof(string))
                            {
                                var colFilter = $"[{EscapeColumnName(col.ColumnName)}] LIKE '%{s}%'";
                                table.DefaultView.RowFilter = colFilter;
                            }
                            else if (col.DataType == typeof(char) || col.DataType == typeof(char?))
                            {
                                if (query.Length == 1)
                                    table.DefaultView.RowFilter = $"UPPER([{EscapeColumnName(col.ColumnName)}]) = '{query.ToUpperInvariant()}'";
                                else
                                    table.DefaultView.RowFilter = $"[{EscapeColumnName(col.ColumnName)}] LIKE '%{s}%'";
                            }
                            else if (IsNumericType(col.DataType))
                            {
                                if (decimal.TryParse(query, out var num))
                                {
                                    table.DefaultView.RowFilter = $"[{EscapeColumnName(col.ColumnName)}] = {num}";
                                }
                                else
                                {
                                    table.DefaultView.RowFilter = $"CONVERT([{EscapeColumnName(col.ColumnName)}], 'System.String') LIKE '%{s}%'";
                                }
                            }
                            else if (col.DataType == typeof(DateTime) || col.DataType == typeof(DateTime?))
                            {
                                if (DateTime.TryParse(query, out var d))
                                {
                                    table.DefaultView.RowFilter = $"[{EscapeColumnName(col.ColumnName)}] = '{d:yyyy-MM-dd}'";
                                }
                                else
                                    table.DefaultView.RowFilter = $"CONVERT([{EscapeColumnName(col.ColumnName)}], 'System.String') LIKE '%{s}%'";
                            }
                            else
                            {
                                table.DefaultView.RowFilter = $"CONVERT([{EscapeColumnName(col.ColumnName)}], 'System.String') LIKE '%{s}%'";
                            }

                            dgv.ClearSelection();
                            return;
                        }
                    }

                    var textCols = table.Columns.Cast<DataColumn>().Where(c => c.DataType == typeof(string)).Select(c => c.ColumnName).ToArray();

                    string[] parts;
                    if (textCols.Length > 0)
                    {
                        parts = textCols.Select(c => $"[{EscapeColumnName(c)}] LIKE '%{s}%'").ToArray();
                    }
                    else
                    {
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
                        if (!string.IsNullOrEmpty(columnName))
                        {
                            var gridCol = dgv.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));
                            if (gridCol != null)
                            {
                                var cellVal = r.Cells[gridCol.Index]?.Value;
                                if (cellVal != null && cellVal != DBNull.Value && Convert.ToString(cellVal)!.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                                    matched = true;
                            }
                        }
                        else
                        {
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
                        }
                        r.Visible = matched;
                    }
                }
            }
            else
            {
                // Фильтруем вручную по указанной колонке или по всем видимым колонках/ячейках
                foreach (DataGridViewRow r in dgv.Rows)
                {
                    if (r.IsNewRow) { r.Visible = false; continue; }
                    bool matched = false;

                    if (!string.IsNullOrEmpty(columnName))
                    {
                        var gridCol = dgv.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));
                        if (gridCol != null)
                        {
                            var val = r.Cells[gridCol.Index]?.Value;
                            if (val != null && val != DBNull.Value && Convert.ToString(val)!.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                                matched = true;
                        }
                    }
                    else
                    {
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
                    }

                    r.Visible = matched;
                }
            }

            dgv.ClearSelection();
            try { if (dgv.CurrentCell != null) dgv.CurrentCell = null; } catch { }
        }

        private static bool IsNumericType(Type t)
        {
            var nt = Nullable.GetUnderlyingType(t) ?? t;
            return nt == typeof(byte) || nt == typeof(sbyte) || nt == typeof(short) || nt == typeof(ushort) ||
                   nt == typeof(int) || nt == typeof(uint) || nt == typeof(long) || nt == typeof(ulong) ||
                   nt == typeof(float) || nt == typeof(double) || nt == typeof(decimal);
        }

        private static string EscapeColumnName(string name)
        {
            // В DataView RowFilter имена колонок в квадратных скобках; удваиваем закрывающую скобку внутри имени
            return (name ?? string.Empty).Replace("]", "]]");
        }

        // ---------------------- Helpers для подключения/отключения ----------------------

        // Загружает все таблицы сразу
        private void LoadAllTables()
        {
            try
            {
                LoadStudentsToGrid();
                LoadTeachersToGrid();
                LoadFullJournalToGrid();
                LoadCurriculumToGrid();
                LoadAchievementsToGrid();
                LoadClassDetailsToGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при загрузке таблиц:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Универсальная попытка подключения и вызов загрузки всех таблиц (если loadAction == null — грузим все)
        private void TryConnectAndLoad(Action? loadAction = null)
        {
            try
            {
                using var conn = global::kursovaya.FirebirdDb.CreateConnection(_dbPath);
                conn.Open();
                try
                {
                    FirebirdDb.EnsureAutoddl(conn);
                }
                catch { /* ignore */ }

                _isConnected = true;
                MessageBox.Show(this, "Подключение к базе данных успешно.", "Подключено", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (loadAction != null)
                    loadAction();
                else
                    LoadAllTables();
            }
            catch (Exception ex)
            {
                _isConnected = false;
                MessageBox.Show(this, $"Ошибка подключения к базе данных:\n{ex.Message}", "Ошибка подключения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Очистить все DataGridView и pictureBox'ы — используется при полном отключении
        private void DisconnectAll()
        {
            // Список имён DataGridView
            var dgvNames = new[] { "dataGridView1", "dataGridView2", "dataGridView3", "dataGridView4", "dataGridView5", "dataGridView6" };
            foreach (var name in dgvNames)
            {
                try
                {
                    var ctrl = FindControlRecursive(this, name) as DataGridView;
                    if (ctrl != null)
                    {
                        ctrl.DataSource = null;
                        ctrl.Columns.Clear();
                    }
                }
                catch { /* ignore individual failures */ }
            }

            // Очистить pictureBox'ы
            var pictureBoxes = new[] { pictureBox1, pictureBox2, pictureBox3 };
            foreach (var pb in pictureBoxes)
            {
                if (pb == null) continue;
                try
                {
                    var prev = pb.Image;
                    pb.Image = null;
                    prev?.Dispose();
                }
                catch { /* ignore */ }
            }

            _isConnected = false;
        }

        // Универсальное отключение — подтверждение и очистка всего UI, если подтверждено
        private void DisconnectAndClear(string? dgvName = null, PictureBox? pb = null)
        {
            // уточнённый текст подтверждения
            var msg = "Вы уверены, что хотите отключиться от базы данных? Все представления данных будут очищены.";
            if (MessageBox.Show(this, msg, "Подтверждение отключения", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            // Если указан конкретный dgvName/пикчер — всё равно очищаем всю программу, чтобы не осталось "подключённых" вкладок
            DisconnectAll();

            MessageBox.Show(this, "Отключено. Все таблицы очищены.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------------------- TabPage1 (ученики) ----------------------

        private void button10_Click(object? sender, EventArgs e) // подключиться (ученики)
        {
            TryConnectAndLoad();
        }

        private void button9_Click(object? sender, EventArgs e) // отключиться (ученики)
        {
            DisconnectAndClear();
        }

        // Поиск — ученики (кнопка)
        private void button8_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView1") as DataGridView;
            if (dgv == null) return;
            var column = ShowSearchColumnDialog(dgv);
            FilterGridByText(dgv, textBox1?.Text, column);
        }

        // Поиск по вводу — можно выполнять и на ввод (необязательно)
        private void textBox1_TextChanged(object? sender, EventArgs e)
        {
            // автопоиск отключён — используйте кнопку "поиск".
        }

        // Обработчик клика по фото ученика — загрузка изображения
        private void pictureBox1_Click(object? sender, EventArgs e)
        {
            SelectImageForPictureBox(pictureBox1);
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

        // Обработчик клика по ячейке грида учеников (пустой — совпадает со стилем остальных обработчиков)
        private void dataGridView1_CellContentClick(object? sender, DataGridViewCellEventArgs e) { }

        // ---------------------- TabPage2 (учителя) ----------------------

        private void button20_Click(object? sender, EventArgs e) // подключиться (учителя)
        {
            TryConnectAndLoad();
        }

        private void button19_Click(object? sender, EventArgs e) // отключиться (учителя)
        {
            DisconnectAndClear();
        }

        // Обработчик клика по фото учителя — загрузка изображения
        private void pictureBox2_Click(object? sender, EventArgs e)
        {
            SelectImageForPictureBox(pictureBox2);
        }

        // Поиск — учителя (кнопка)
        private void button18_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView2") as DataGridView;
            if (dgv == null) return;
            var column = ShowSearchColumnDialog(dgv);
            FilterGridByText(dgv, textBox2?.Text, column);
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

        // Сортировать (учителя)
        private void button15_Click(object? sender, EventArgs e)
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

        private void button30_Click(object? sender, EventArgs e) // подключиться (журнал)
        {
            TryConnectAndLoad();
        }

        private void button29_Click(object? sender, EventArgs e) // отключиться (журнал)
        {
            DisconnectAndClear();
        }

        // Поиск — журнал (кнопка)
        private void button28_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView3") as DataGridView;
            if (dgv == null) return;
            var column = ShowSearchColumnDialog(dgv);
            FilterGridByText(dgv, textBox3?.Text, column);
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

        private void button24_Click(object sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView3");
                if (!(ctrl is DataGridView dgv)) return;
                if (dgv.DataSource is not DataTable dt)
                {
                    MessageBox.Show(this, "Нет данных для сохранения.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var changes = dt.GetChanges();
                var anySaved = false;

                if (changes != null)
                {
                    foreach (DataRow row in changes.Rows)
                    {
                        if (row.RowState != DataRowState.Modified) continue;

                        string? studentFull = row.Table.Columns.Contains("STUDENT_FULLNAME") && row["STUDENT_FULLNAME"] != DBNull.Value
                            ? Convert.ToString(row["STUDENT_FULLNAME"])
                            : null;
                        string? subjectName = row.Table.Columns.Contains("SUBJECT_NAME") && row["SUBJECT_NAME"] != DBNull.Value
                            ? Convert.ToString(row["SUBJECT_NAME"])
                            : null;
                        DateTime? lessonDate = row.Table.Columns.Contains("LESSON_DATE") && row["LESSON_DATE"] != DBNull.Value
                            ? (DateTime?)Convert.ToDateTime(row["LESSON_DATE"])
                            : null;

                        // возможно пользователь изменил оценку или статус присутствия
                        int? gradeVal = row.Table.Columns.Contains("GRADE") && row["GRADE"] != DBNull.Value
                            ? (int?)Convert.ToInt32(row["GRADE"])
                            : null;
                        string? presenceStatus = row.Table.Columns.Contains("PRESENCE_STATUS") && row["PRESENCE_STATUS"] != DBNull.Value
                            ? Convert.ToString(row["PRESENCE_STATUS"])
                            : null;

                        if (string.IsNullOrWhiteSpace(studentFull) || string.IsNullOrWhiteSpace(subjectName) || lessonDate == null)
                            continue;

                        // Найдём student_id
                        var dtStudent = FirebirdDb.ExecuteQuery(_dbPath,
                            "SELECT STUDENT_ID FROM STUDENTS WHERE TRIM(LASTNAME || ' ' || FIRSTNAME) = @FULL OR TRIM(LASTNAME || ' ' || FIRSTNAME || ' ' || COALESCE(MIDDLENAME,'')) = @FULL",
                            new FbParameter("FULL", FbDbType.VarChar) { Value = studentFull.Trim() });
                        if (dtStudent.Rows.Count == 0) continue;
                        int studentId = Convert.ToInt32(dtStudent.Rows[0]["STUDENT_ID"]);

                        // Найдём subject_id
                        var dtSub = FirebirdDb.ExecuteQuery(_dbPath, "SELECT SUBJECT_ID FROM SUBJECTS WHERE NAME = @NAME",
                            new FbParameter("NAME", FbDbType.VarChar) { Value = subjectName.Trim() });
                        if (dtSub.Rows.Count == 0) continue;
                        int subjectId = Convert.ToInt32(dtSub.Rows[0]["SUBJECT_ID"]);

                        var dateOnly = lessonDate.Value.Date;

                        // Обновление/добавление оценки
                        if (gradeVal != null)
                        {
                            var dtG = FirebirdDb.ExecuteQuery(_dbPath,
                                @"SELECT GRADE_ID FROM GRADES g
                          WHERE g.STUDENT_ID = @SID AND g.SUBJECT_ID = @SUB AND g.GRADE_DATE = @GDATE",
                                new FbParameter("SID", FbDbType.Integer) { Value = studentId },
                                new FbParameter("SUB", FbDbType.Integer) { Value = subjectId },
                                new FbParameter("GDATE", FbDbType.Date) { Value = dateOnly });

                            if (dtG.Rows.Count > 0)
                            {
                                int gradeId = Convert.ToInt32(dtG.Rows[0]["GRADE_ID"]);
                                DbProcedures.UpdateGrade(_dbPath, gradeId, gradeVal.Value, dateOnly);
                                anySaved = true;
                            }
                            else
                            {
                                // если записи оценки нет — создадим новую
                                DbProcedures.AddGrade(_dbPath, studentId, subjectId, gradeVal.Value, dateOnly);
                                anySaved = true;
                            }
                        }

                        // Обновление/добавление посещаемости (presence)
                        if (!string.IsNullOrWhiteSpace(presenceStatus))
                        {
                            var presChar = presenceStatus.Trim()[0];

                            var dtA = FirebirdDb.ExecuteQuery(_dbPath,
                                @"SELECT ATTENDANCE_ID FROM ATTENDANCES a
                          WHERE a.STUDENT_ID = @SID AND a.SUBJECT_ID = @SUB AND a.LESSON_DATE = @LDATE",
                                new FbParameter("SID", FbDbType.Integer) { Value = studentId },
                                new FbParameter("SUB", FbDbType.Integer) { Value = subjectId },
                                new FbParameter("LDATE", FbDbType.Date) { Value = dateOnly });

                            if (dtA.Rows.Count > 0)
                            {
                                int attId = Convert.ToInt32(dtA.Rows[0]["ATTENDANCE_ID"]);
                                DbProcedures.UpdateAttendance(_dbPath, attId, presChar, dateOnly);
                                anySaved = true;
                            }
                            else
                            {
                                // если записи посещаемости нет — создадим новую
                                DbProcedures.AddAttendance(_dbPath, studentId, subjectId, dateOnly, presChar);
                                anySaved = true;
                            }
                        }
                    }
                }

                if (!anySaved)
                {
                    MessageBox.Show(this, "Изменений нет.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                LoadFullJournalToGrid();
                MessageBox.Show(this, "Журнал сохранён.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при сохранении журнала:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

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

        private void button40_Click(object? sender, EventArgs e) // подключиться (учебный план)
        {
            TryConnectAndLoad();
        }

        private void button39_Click(object? sender, EventArgs e) // отключиться (учебный план)
        {
            DisconnectAndClear();
        }

        // Поиск — учебный план (кнопка)
        private void button38_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView4") as DataGridView;
            if (dgv == null) return;
            var column = ShowSearchColumnDialog(dgv);
            FilterGridByText(dgv, textBox4?.Text, column);
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

        private void button50_Click(object? sender, EventArgs e) // подключиться (достижения)
        {
            TryConnectAndLoad();
        }

        private void button49_Click(object? sender, EventArgs e) // отключиться (достижения)
        {
            DisconnectAndClear();
        }

        // Поиск — достижения (кнопка)
        private void button48_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView5") as DataGridView;
            if (dgv == null) return;
            var column = ShowSearchColumnDialog(dgv);
            FilterGridByText(dgv, textBox5?.Text, column);
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

        // Реализован выбор фото для вкладки "достижения"
        private void pictureBox3_Click(object? sender, EventArgs e)
        {
            SelectImageForPictureBox(pictureBox3);
        }

        private void dataGridView5_CellContentClick(object? sender, DataGridViewCellEventArgs e) { }

        // ---------------------- TabPage6 (классы) ----------------------

        private void button60_Click(object? sender, EventArgs e) // подключиться (классы)
        {
            TryConnectAndLoad();
        }

        private void button59_Click(object? sender, EventArgs e) // отключиться (классы)
        {
            DisconnectAndClear();
        }

        // Поиск — классы (кнопка)
        private void button58_Click(object? sender, EventArgs e)
        {
            var dgv = FindControlRecursive(this, "dataGridView6") as DataGridView;
            if (dgv == null) return;
            var column = ShowSearchColumnDialog(dgv);
            FilterGridByText(dgv, textBox6?.Text, column);
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