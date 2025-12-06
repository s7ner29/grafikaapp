using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FirebirdSql.Data.FirebirdClient;

namespace kursovaya
{
    public partial class Form1 : Form
    {
        private readonly string _dbPath = @"C:\Users\S7ner\Desktop\dnevnik\shkila.FDB";

        public Form1()
        {
            InitializeComponent();

            // Растянуть фото на весь элемент и убрать рамку/контрастный фон
            if (pictureBox1 != null)
            {
                pictureBox1.BackColor = SystemColors.Control;
                pictureBox1.BorderStyle = BorderStyle.None;
                pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage; // или PictureBoxSizeMode.Zoom для сохранения пропорций
            }

            // Настройка для pictureBox2: растянуть на весь элемент и убрать фон/рамку
            if (pictureBox2 != null)
            {
                pictureBox2.BackColor = SystemColors.Control;
                pictureBox2.BorderStyle = BorderStyle.None;
                pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
            }

            // Настройка для pictureBox3: растянуть на весь элемент и убрать фон/рамку
            if (pictureBox3 != null)
            {
                pictureBox3.BackColor = SystemColors.Control;
                pictureBox3.BorderStyle = BorderStyle.None;
                pictureBox3.SizeMode = PictureBoxSizeMode.Zoom; // Zoom сохраняет пропорции и масштабирует по размеру pictureBox3
            }

            this.Load += Form1_Load;
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            try
            {
                LoadStudentsToGrid();
                LoadTeachersToGrid();
                LoadFullJournalToGrid(); // добавлено: загрузка журнала в dataGridView3
                LoadCurriculumToGrid(); // добавлено: загрузка учебного плана в dataGridView4
                LoadAchievementsToGrid(); // теперь загружаем процедуру деталей достижений
                LoadClassDetailsToGrid(); // загрузка данных о классах
            }
            catch (FbException fbEx)
            {
                MessageBox.Show(this, $"Ошибка подключения к Firebird:\n{fbEx.Message}", "Ошибка подключения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Загружает список студентов в dataGridView1, скрывает STUDENT_ID и переименовывает заголовки на русский
        private void LoadStudentsToGrid()
        {
            var dt = DbProcedures.CallSelectableProc(_dbPath, "GET_STUDENTS_WITH_CLASS");

            // Найти DataGridView рекурсивно — он может быть внутри TabPage/Panel
            var ctrl = FindControlRecursive(this, "dataGridView1");
            if (!(ctrl is DataGridView dgv))
                return; // не найден — ничего не делаем

            dgv.AutoGenerateColumns = true;
            dgv.DataSource = dt;

            // Скрыть служебный идентификатор (поиск имени столбца без учёта регистра)
            var idCol = dgv.Columns.Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.Name, "STUDENT_ID", StringComparison.OrdinalIgnoreCase));
            if (idCol != null) idCol.Visible = false;

            // Пометка: переименовываем заголовки по нечувствительному к регистру совпадению
            void SetHeader(string colName, string header)
            {
                var col = dgv.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.Name, colName, StringComparison.OrdinalIgnoreCase));
                if (col != null) col.HeaderText = header;
            }

            SetHeader("LASTNAME", "Фамилия");
            SetHeader("FIRSTNAME", "Имя");
            SetHeader("MIDDLENAME", "Отчество");
            SetHeader("GENDER", "Пол");
            SetHeader("BIRTHDATE", "Дата рождения");
            SetHeader("YEAR_ADMISSION", "Год поступления");
            SetHeader("CLASS_NAME", "Класс");
            SetHeader("PHONE", "Телефон");
            SetHeader("EMAIL", "Email");

            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.ClearSelection();

            dgv.SelectionChanged -= DataGridView1_SelectionChanged;
            dgv.SelectionChanged += DataGridView1_SelectionChanged;

            // Отключаем стандартную синюю подсветку при выборе строки/ячейки
            dgv.EnableHeadersVisualStyles = false;

            // базовый фон для ячеек
            var cellBack = dgv.DefaultCellStyle.BackColor;
            if (cellBack == Color.Empty) cellBack = SystemColors.Window;
            dgv.DefaultCellStyle.SelectionBackColor = cellBack;
            dgv.DefaultCellStyle.SelectionForeColor = dgv.DefaultCellStyle.ForeColor;

            // заголовки колонок — оставляем их фон неизменным при выборе
            var headerBack = dgv.ColumnHeadersDefaultCellStyle.BackColor;
            if (headerBack == Color.Empty) headerBack = SystemColors.Control;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBack;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;

            // заголовки строк (левый столбец) — аналогично
            var rowHeaderBack = dgv.RowHeadersDefaultCellStyle.BackColor;
            if (rowHeaderBack == Color.Empty) rowHeaderBack = cellBack;
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = rowHeaderBack;
            dgv.RowHeadersDefaultCellStyle.SelectionForeColor = dgv.RowHeadersDefaultCellStyle.ForeColor;

            // Убедиться, что текущая ячейка не применяет отдельную подсветку
            var curStyle = dgv.CurrentCell?.Style;
            if (curStyle != null)
            {
                curStyle.SelectionBackColor = cellBack;
                curStyle.SelectionForeColor = curStyle.ForeColor;
            }
        }

        // Загружает список учителей в dataGridView2 напрямую из таблицы TEACHERS (отдельные поля, не объединять)
        private void LoadTeachersToGrid()
        {
            // Получаем столбцы такими же, как в базе (TEACHER_ID, LASTNAME, FIRSTNAME, MIDDLENAME, GENDER, JOB_TITLE, PHONE, PHOTO)
            var dt = global::kursovaya.FirebirdDb.ExecuteQuery(_dbPath,
                "SELECT TEACHER_ID, LASTNAME, FIRSTNAME, MIDDLENAME, GENDER, JOB_TITLE, PHONE, PHOTO FROM TEACHERS ORDER BY TEACHER_ID");

            var ctrl = FindControlRecursive(this, "dataGridView2");
            if (!(ctrl is DataGridView dgv))
                return; // не найден — ничего не делаем

            dgv.AutoGenerateColumns = true;
            dgv.DataSource = dt;

            // Скрыть служебный идентификатор и столбец с фото (фото показываем отдельно в pictureBox2)
            var idCol = dgv.Columns.Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.Name, "TEACHER_ID", StringComparison.OrdinalIgnoreCase));
            if (idCol != null) idCol.Visible = false;

            var photoCol = dgv.Columns.Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.Name, "PHOTO", StringComparison.OrdinalIgnoreCase));
            if (photoCol != null) photoCol.Visible = false;

            // Переименовать заголовки колонок на русский (по отдельным полям)
            void SetHeader(string colName, string header)
            {
                var col = dgv.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.Name, colName, StringComparison.OrdinalIgnoreCase));
                if (col != null) col.HeaderText = header;
            }

            SetHeader("LASTNAME", "Фамилия");
            SetHeader("FIRSTNAME", "Имя");
            SetHeader("MIDDLENAME", "Отчество");
            SetHeader("GENDER", "Пол");
            SetHeader("JOB_TITLE", "Должность");
            SetHeader("PHONE", "Телефон");

            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.ClearSelection();

            dgv.SelectionChanged -= DataGridView2_SelectionChanged;
            dgv.SelectionChanged += DataGridView2_SelectionChanged;

            // Отключаем стандартную синюю подсветку при выборе строки/ячейки
            dgv.EnableHeadersVisualStyles = false;
            var cellBack = dgv.DefaultCellStyle.BackColor;
            if (cellBack == Color.Empty) cellBack = SystemColors.Window;
            dgv.DefaultCellStyle.SelectionBackColor = cellBack;
            dgv.DefaultCellStyle.SelectionForeColor = dgv.DefaultCellStyle.ForeColor;

            var headerBack = dgv.ColumnHeadersDefaultCellStyle.BackColor;
            if (headerBack == Color.Empty) headerBack = SystemColors.Control;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBack;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;

            var rowHeaderBack = dgv.RowHeadersDefaultCellStyle.BackColor;
            if (rowHeaderBack == Color.Empty) rowHeaderBack = cellBack;
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = rowHeaderBack;
            dgv.RowHeadersDefaultCellStyle.SelectionForeColor = dgv.RowHeadersDefaultCellStyle.ForeColor;

            var curStyle = dgv.CurrentCell?.Style;
            if (curStyle != null)
            {
                curStyle.SelectionBackColor = cellBack;
                curStyle.SelectionForeColor = curStyle.ForeColor;
            }
        }

        // Загружает полный журнал (GET_FULL_JOURNAL) в dataGridView3 и переводит заголовки на русский
        private void LoadFullJournalToGrid()
        {
            var dt = DbProcedures.CallSelectableProc(_dbPath, "GET_FULL_JOURNAL");

            var ctrl = FindControlRecursive(this, "dataGridView3");
            if (!(ctrl is DataGridView dgv))
                return;

            // Пряжа таблицу на время перестройки, чтобы не было заметного "перетягивания" колонок при запуске
            dgv.Visible = false;

            dgv.AutoGenerateColumns = true;
            dgv.DataSource = dt;

            // Снятие автоматического Fill — сначала подгоняем ширины под содержимое
            dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgv.RowTemplate.Height = 24;
            dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False;

            // Переименовать заголовки колонок
            void SetHeader(string colName, string header)
            {
                var col = dgv.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.Name, colName, StringComparison.OrdinalIgnoreCase));
                if (col != null) col.HeaderText = header;
            }

            SetHeader("STUDENT_FULLNAME", "ФИО ученика");
            SetHeader("CLASS_NAME", "Класс");
            SetHeader("SUBJECT_NAME", "Предмет");
            SetHeader("TEACHER_FULLNAME", "Учитель");
            SetHeader("GRADE", "Оценка");
            SetHeader("LESSON_DATE", "Дата занятия");
            SetHeader("PRESENCE_STATUS", "Присутствие");

            // Автоматически подогнать ширины по содержимому (быстро и без постоянного "растяжения" в цикле)
            dgv.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);

            // Обеспечить минимальные ширины и подрезать самого "жирного" столбца (ФИО), если итоговая ширина больше доступной области
            int available = dgv.ClientSize.Width - dgv.RowHeadersWidth - SystemInformation.VerticalScrollBarWidth - 6;
            if (available < 200) available = dgv.ClientSize.Width - dgv.RowHeadersWidth - 6;

            // Определяем минимумы
            Func<string, int> minW = name =>
            {
                return name switch
                {
                    "STUDENT_FULLNAME" => 120,
                    "TEACHER_FULLNAME" => 100,
                    "SUBJECT_NAME" => 80,
                    "LESSON_DATE" => 80,
                    "CLASS_NAME" => 50,
                    "GRADE" => 40,
                    "PRESENCE_STATUS" => 30,
                    _ => 50
                };
            };

            var cols = dgv.Columns.Cast<DataGridViewColumn>().ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
            // суммируем текущие ширины
            int totalWidth = cols.Values.Sum(c => c.Width);

            if (totalWidth > available)
            {
                // Оставим все короткие колонки как есть, а сократим ФИО в последнюю очередь
                int otherWidth = cols.Values.Where(c => !string.Equals(c.Name, "STUDENT_FULLNAME", StringComparison.OrdinalIgnoreCase)).Sum(c => c.Width);
                int targetFullname = Math.Max(minW("STUDENT_FULLNAME"), available - otherWidth);
                if (cols.TryGetValue("STUDENT_FULLNAME", out var fullnameCol))
                {
                    fullnameCol.Width = targetFullname;
                }

                // Если всё ещё больше — уменьшать другие ненужные колонки по минимуму справа-налево
                totalWidth = cols.Values.Sum(c => c.Width);
                if (totalWidth > available)
                {
                    var adjustable = new[] { "TEACHER_FULLNAME", "SUBJECT_NAME", "LESSON_DATE", "CLASS_NAME", "GRADE", "PRESENCE_STATUS" };
                    foreach (var name in adjustable)
                    {
                        if (!cols.TryGetValue(name, out var c)) continue;
                        int min = minW(name);
                        int extra = totalWidth - available;
                        int reducible = Math.Max(0, c.Width - min);
                        int reduceBy = Math.Min(reducible, extra);
                        c.Width -= reduceBy;
                        totalWidth -= reduceBy;
                        if (totalWidth <= available) break;
                    }
                }
            }
            else
            {
                // Если места больше чем нужно — дать немного дополнительного места ФИО
                if (cols.TryGetValue("STUDENT_FULLNAME", out var fullnameCol))
                {
                    int extra = available - totalWidth;
                    int give = Math.Min(200, extra); // не даём слишком много
                    fullnameCol.Width += give;
                }
            }

            // Скрывать/фиксировать автопересчёт, чтобы при дальнейших действиях столбцы не "прыга ли"
            foreach (DataGridViewColumn c in dgv.Columns)
            {
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                // при желании можно запретить изменение пользователем:
                // c.Resizable = DataGridViewTriState.True;
            }

            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.ClearSelection();

            dgv.SelectionChanged -= DataGridView3_SelectionChanged;
            dgv.SelectionChanged += DataGridView3_SelectionChanged;

            // Отключаем синюю подсветку выбора (делаем выбор нейтральным)
            dgv.EnableHeadersVisualStyles = false;
            var cellBack = dgv.DefaultCellStyle.BackColor;
            if (cellBack == Color.Empty) cellBack = SystemColors.Window;
            dgv.DefaultCellStyle.SelectionBackColor = cellBack;
            dgv.DefaultCellStyle.SelectionForeColor = dgv.DefaultCellStyle.ForeColor;

            var headerBack = dgv.ColumnHeadersDefaultCellStyle.BackColor;
            if (headerBack == Color.Empty) headerBack = SystemColors.Control;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBack;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;

            var rowHeaderBack = dgv.RowHeadersDefaultCellStyle.BackColor;
            if (rowHeaderBack == Color.Empty) rowHeaderBack = cellBack;
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = rowHeaderBack;
            dgv.RowHeadersDefaultCellStyle.SelectionForeColor = dgv.RowHeadersDefaultCellStyle.ForeColor;

            var curStyle = dgv.CurrentCell?.Style;
            if (curStyle != null)
            {
                curStyle.SelectionBackColor = cellBack;
                curStyle.SelectionForeColor = curStyle.ForeColor;
            }

            // Показать таблицу после всех операций, чтобы избежать визуального "перетягивания"
            dgv.Visible = true;
        }

        // Загружает учебный план (GET_CURRICULUM) в dataGridView4 и переводит заголовки на русский
        private void LoadCurriculumToGrid()
        {
            var dt = DbProcedures.CallSelectableProc(_dbPath, "GET_CURRICULUM");

            var ctrl = FindControlRecursive(this, "dataGridView4");
            if (!(ctrl is DataGridView dgv))
                return;

            dgv.AutoGenerateColumns = true;
            dgv.DataSource = dt;

            void SetHeader(string colName, string header)
            {
                var col = dgv.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.Name, colName, StringComparison.OrdinalIgnoreCase));
                if (col != null) col.HeaderText = header;
            }

            SetHeader("CLASS_NAME", "Класс");
            SetHeader("SUBJECT_NAME", "Предмет");
            SetHeader("TEACHER_FULLNAME", "Учитель");
            SetHeader("HOURS", "Часы");

            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.ClearSelection();

            dgv.SelectionChanged -= DataGridView4_SelectionChanged;
            dgv.SelectionChanged += DataGridView4_SelectionChanged;

            // Сделать выбор нейтральным (убрать синий фон)
            dgv.EnableHeadersVisualStyles = false;
            var cellBack = dgv.DefaultCellStyle.BackColor;
            if (cellBack == Color.Empty) cellBack = SystemColors.Window;
            dgv.DefaultCellStyle.SelectionBackColor = cellBack;
            dgv.DefaultCellStyle.SelectionForeColor = dgv.DefaultCellStyle.ForeColor;

            var headerBack = dgv.ColumnHeadersDefaultCellStyle.BackColor;
            if (headerBack == Color.Empty) headerBack = SystemColors.Control;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBack;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;

            var rowHeaderBack = dgv.RowHeadersDefaultCellStyle.BackColor;
            if (rowHeaderBack == Color.Empty) rowHeaderBack = cellBack;
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = rowHeaderBack;
            dgv.RowHeadersDefaultCellStyle.SelectionForeColor = dgv.RowHeadersDefaultCellStyle.ForeColor;
        }

        // Обработчик выбора строки в dataGridView4 (пустой — можно расширить при необходимости)
        private void DataGridView4_SelectionChanged(object? sender, EventArgs e)
        {
            // заглушка — логика при выборе записи учебного плана при необходимости
        }

        // Обработчик выбора строки в dataGridView3 (пустой — можно расширить при необходимости)
        private void DataGridView3_SelectionChanged(object? sender, EventArgs e)
        {
            // Здесь можно добавить логику при выборе записи журнала (например, показать подробности)
        }

        // Загружает детали достижений студентов из процедуры GET_STUDENT_ACHIEVEMENTS_DETAIL в dataGridView5
        private void LoadAchievementsToGrid()
        {
            var dt = DbProcedures.CallSelectableProc(_dbPath, "GET_STUDENT_ACHIEVEMENTS_DETAIL");

            var ctrl = FindControlRecursive(this, "dataGridView5");
            if (!(ctrl is DataGridView dgv))
                return;

            dgv.AutoGenerateColumns = true;
            dgv.DataSource = dt;

            // Скрыть BLOB-столбец с фото (отображаем отдельно в pictureBox3)
            var blobCol = dgv.Columns.Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.Name, "PROOF_PHOTO", StringComparison.OrdinalIgnoreCase));
            if (blobCol != null) blobCol.Visible = false;

            // Переименовать заголовки колонок на русский (раздельные ФИО)
            void SetHeader(string colName, string header)
            {
                var col = dgv.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.Name, colName, StringComparison.OrdinalIgnoreCase));
                if (col != null) col.HeaderText = header;
            }

            SetHeader("LASTNAME", "Фамилия");
            SetHeader("FIRSTNAME", "Имя");
            SetHeader("MIDDLENAME", "Отчество");
            SetHeader("TITLE", "Название");
            SetHeader("EVENT_DATE", "Дата события");
            SetHeader("LEVEL", "Уровень");
            SetHeader("DESCRIPTION", "Описание");

            // Отключаем синюю подсветку и делаем выбор нейтральным
            dgv.EnableHeadersVisualStyles = false;
            var cellBack = dgv.DefaultCellStyle.BackColor;
            if (cellBack == Color.Empty) cellBack = SystemColors.Window;
            dgv.DefaultCellStyle.SelectionBackColor = cellBack;
            dgv.DefaultCellStyle.SelectionForeColor = dgv.DefaultCellStyle.ForeColor;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = dgv.ColumnHeadersDefaultCellStyle.BackColor == Color.Empty ? SystemColors.Control : dgv.ColumnHeadersDefaultCellStyle.BackColor;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;

            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;
            dgv.ClearSelection();

            // Подписываем корректный обработчик для dataGridView5
            dgv.SelectionChanged -= DataGridView5_SelectionChanged;
            dgv.SelectionChanged += DataGridView5_SelectionChanged;
        }

        // Рекурсивный поиск контролa по имени
        private Control? FindControlRecursive(Control parent, string name)
        {
            if (parent == null) return null;
            if (string.Equals(parent.Name, name, StringComparison.OrdinalIgnoreCase)) return parent;

            foreach (Control child in parent.Controls)
            {
                var found = FindControlRecursive(child, name);
                if (found != null) return found;
            }

            return null;
        }

        private void DataGridView1_SelectionChanged(object? sender, EventArgs e)
        {
            if (!(sender is DataGridView dgv))
            {
                pictureBox1.Image = null;
                return;
            }

            if (dgv.CurrentRow == null)
            {
                pictureBox1.Image = null;
                return;
            }

            var drv = dgv.CurrentRow.DataBoundItem as DataRowView;
            if (drv == null)
            {
                pictureBox1.Image = null;
                return;
            }

            // Найти колонку STUDENT_ID в таблице результатов (без учёта регистра)
            var dtCol = drv.Row.Table.Columns
                .Cast<DataColumn>()
                .FirstOrDefault(c => string.Equals(c.ColumnName, "STUDENT_ID", StringComparison.OrdinalIgnoreCase));

            if (dtCol == null)
            {
                pictureBox1.Image = null;
                return;
            }

            var idObj = drv.Row[dtCol];
            if (idObj == DBNull.Value || idObj == null)
            {
                pictureBox1.Image = null;
                return;
            }

            if (!int.TryParse(idObj.ToString(), out var studentId))
            {
                pictureBox1.Image = null;
                return;
            }

            LoadStudentPhoto(studentId);
        }

        private void DataGridView2_SelectionChanged(object? sender, EventArgs e)
        {
            if (!(sender is DataGridView dgv))
            {
                pictureBox2.Image = null;
                return;
            }

            if (dgv.CurrentRow == null)
            {
                pictureBox2.Image = null;
                return;
            }

            var drv = dgv.CurrentRow.DataBoundItem as DataRowView;
            if (drv == null)
            {
                pictureBox2.Image = null;
                return;
            }

            // Найти колонку TEACHER_ID в таблице результатов (без учёта регистра)
            var dtCol = drv.Row.Table.Columns
                .Cast<DataColumn>()
                .FirstOrDefault(c => string.Equals(c.ColumnName, "TEACHER_ID", StringComparison.OrdinalIgnoreCase));

            if (dtCol == null)
            {
                pictureBox2.Image = null;
                return;
            }

            var idObj = drv.Row[dtCol];
            if (idObj == DBNull.Value || idObj == null)
            {
                pictureBox2.Image = null;
                return;
            }

            if (!int.TryParse(idObj.ToString(), out var teacherId))
            {
                pictureBox2.Image = null;
                return;
            }

            LoadTeacherPhoto(teacherId);
        }

        // Загружает фото студента из таблицы STUDENTS и ставит в pictureBox1
        private void LoadStudentPhoto(int studentId)
        {
            try
            {
                var param = new FbParameter("P_STUDENT_ID", FbDbType.Integer) { Value = studentId };
                var dt = global::kursovaya.FirebirdDb.ExecuteQuery(_dbPath, "SELECT PHOTO FROM STUDENTS WHERE STUDENT_ID = @P_STUDENT_ID", param);

                if (dt.Rows.Count == 0)
                {
                    pictureBox1.Image = null;
                    return;
                }

                var val = dt.Rows[0][0];
                if (val == DBNull.Value || val == null)
                {
                    pictureBox1.Image = null;
                    return;
                }

                byte[]? blob = val as byte[];
                if (blob == null && val is System.Data.Common.DbDataRecord rec && rec[0] is byte[] b)
                    blob = b;

                if (blob == null || blob.Length == 0)
                {
                    pictureBox1.Image = null;
                    return;
                }

                pictureBox1.Image = DbProcedures.BlobToImage(blob);
            }
            catch
            {
                pictureBox1.Image = null;
            }
        }

        // Загружает фото учителя из таблицы TEACHERS и ставит в pictureBox2
        private void LoadTeacherPhoto(int teacherId)
        {
            try
            {
                var param = new FbParameter("P_TEACHER_ID", FbDbType.Integer) { Value = teacherId };
                var dt = global::kursovaya.FirebirdDb.ExecuteQuery(_dbPath, "SELECT PHOTO FROM TEACHERS WHERE TEACHER_ID = @P_TEACHER_ID", param);

                if (dt.Rows.Count == 0)
                {
                    pictureBox2.Image = null;
                    return;
                }

                var val = dt.Rows[0][0];
                if (val == DBNull.Value || val == null)
                {
                    pictureBox2.Image = null;
                    return;
                }

                byte[]? blob = val as byte[];
                if (blob == null && val is System.Data.Common.DbDataRecord rec && rec[0] is byte[] b)
                    blob = b;

                if (blob == null || blob.Length == 0)
                {
                    pictureBox2.Image = null;
                    return;
                }

                pictureBox2.Image = DbProcedures.BlobToImage(blob);
            }
            catch
            {
                pictureBox2.Image = null;
            }
        }

        private void ShowDataTableInDialog(DataTable dt, string title)
        {
            using var resultForm = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                Width = 600,
                Height = 300
            };

            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DataSource = dt,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            resultForm.Controls.Add(dgv);
            resultForm.ShowDialog(this);
        }

      

        private void button1_Click(object sender, EventArgs e) // добавить ученика
        {
            using var dlg = new AddStudentDialog(_dbPath);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                LoadStudentsToGrid();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView1");
                if (!(ctrl is DataGridView dgv)) return;
                if (dgv.DataSource is not DataTable dt) { MessageBox.Show(this, "Нет данных для сохранения.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

                var changes = dt.GetChanges();
                if (changes == null)
                {
                    // рассмотрим отдельно фото текущего выбранного ученика
                    var curId = GetSelectedIdFromGrid(dgv, "STUDENT_ID");
                    if (curId != null && pictureBox1.Image != null)
                    {
                        var blob = DbProcedures.ImageToBlob(pictureBox1.Image);
                        if (blob != null)
                            DbProcedures.UpdateStudentPhoto(_dbPath, curId.Value, blob);
                    }
                    MessageBox.Show(this, "Изменений нет.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                foreach (DataRow row in changes.Rows)
                {
                    if (row.RowState == DataRowState.Modified)
                    {
                        int studentId = Convert.ToInt32(row["STUDENT_ID"]);
                        string last = Convert.ToString(row["LASTNAME"]) ?? string.Empty;
                        string first = Convert.ToString(row["FIRSTNAME"]) ?? string.Empty;
                        string? middle = row.Table.Columns.Contains("MIDDLENAME") && row["MIDDLENAME"] != DBNull.Value ? Convert.ToString(row["MIDDLENAME"]) : null;
                        char gender = 'M';
                        if (row.Table.Columns.Contains("GENDER") && row["GENDER"] != DBNull.Value)
                        {
                            var g = Convert.ToString(row["GENDER"]) ?? string.Empty;
                            if (!string.IsNullOrEmpty(g)) gender = g[0];
                        }
                        DateTime? birth = null;
                        if (row.Table.Columns.Contains("BIRTHDATE") && row["BIRTHDATE"] != DBNull.Value)
                            birth = Convert.ToDateTime(row["BIRTHDATE"]);
                        int yearAdmission = row.Table.Columns.Contains("YEAR_ADMISSION") && row["YEAR_ADMISSION"] != DBNull.Value ? Convert.ToInt32(row["YEAR_ADMISSION"]) : DateTime.Now.Year;
                        int classId = 0;
                        // В GET_STUDENTS_WITH_CLASS возвращается CLASS_NAME, а не CLASS_ID. Если CLASS_ID отсутствует, пропускаем изменение class.
                        if (row.Table.Columns.Contains("CLASS_ID") && row["CLASS_ID"] != DBNull.Value)
                            classId = Convert.ToInt32(row["CLASS_ID"]);
                        else
                        {
                            // попытка получить CLASS_ID по CLASS_NAME из базы
                            if (row.Table.Columns.Contains("CLASS_NAME") && row["CLASS_NAME"] != DBNull.Value)
                            {
                                var className = Convert.ToString(row["CLASS_NAME"]);
                                var dtClass = global::kursovaya.FirebirdDb.ExecuteQuery(_dbPath, "SELECT CLASS_ID FROM CLASSES WHERE CLASS_NAME = @P_CLASS_NAME",
                                    new FbParameter("P_CLASS_NAME", FbDbType.VarChar) { Value = className });
                                if (dtClass.Rows.Count > 0) classId = Convert.ToInt32(dtClass.Rows[0]["CLASS_ID"]);
                            }
                        }
                        string? phone = row.Table.Columns.Contains("PHONE") && row["PHONE"] != DBNull.Value ? Convert.ToString(row["PHONE"]) : null;
                        string? email = row.Table.Columns.Contains("EMAIL") && row["EMAIL"] != DBNull.Value ? Convert.ToString(row["EMAIL"]) : null;

                        // last and first are non-nullable for DbProcedures.UpdateStudent
                        DbProcedures.UpdateStudent(_dbPath, studentId, last ?? string.Empty, first ?? string.Empty, middle, gender, birth, yearAdmission, classId, phone, email);
                    }
                    // можно обрабатывать добавления/удаления отдельно
                }

                // Фото выбранного студента
                var selId = GetSelectedIdFromGrid(dgv, "STUDENT_ID");
                if (selId != null && pictureBox1.Image != null)
                {
                    var blob = DbProcedures.ImageToBlob(pictureBox1.Image);
                    if (blob != null)
                        DbProcedures.UpdateStudentPhoto(_dbPath, selId.Value, blob);
                }

                LoadStudentsToGrid();
                MessageBox.Show(this, "Изменения сохранены.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при сохранении учеников:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Сохранение изменений в таблице "Учителя" (button13)
        private void button13_Click(object sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView2");
                if (!(ctrl is DataGridView dgv)) return;
                if (dgv.DataSource is not DataTable dt) { MessageBox.Show(this, "Нет данных для сохранения.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

                var changes = dt.GetChanges();
                if (changes != null)
                {
                    foreach (DataRow row in changes.Rows)
                    {
                        if (row.RowState == DataRowState.Modified)
                        {
                            int teacherId = Convert.ToInt32(row["TEACHER_ID"]);
                            string last = Convert.ToString(row["LASTNAME"]) ?? string.Empty;
                            string first = Convert.ToString(row["FIRSTNAME"]) ?? string.Empty;
                            string? middle = row.Table.Columns.Contains("MIDDLENAME") && row["MIDDLENAME"] != DBNull.Value ? Convert.ToString(row["MIDDLENAME"]) : null;
                            char gender = 'M';
                            if (row.Table.Columns.Contains("GENDER") && row["GENDER"] != DBNull.Value)
                            {
                                var g = Convert.ToString(row["GENDER"]) ?? string.Empty;
                                if (!string.IsNullOrEmpty(g)) gender = g[0];
                            }
                            string job = row.Table.Columns.Contains("JOB_TITLE") && row["JOB_TITLE"] != DBNull.Value ? Convert.ToString(row["JOB_TITLE"]) ?? string.Empty : string.Empty;
                            string? phone = row.Table.Columns.Contains("PHONE") && row["PHONE"] != DBNull.Value ? Convert.ToString(row["PHONE"]) : null;

                            DbProcedures.UpdateTeacher(_dbPath, teacherId, last ?? string.Empty, first ?? string.Empty, middle, gender, job ?? string.Empty, phone);
                        }
                    }
                }

                // Фото текущего выбранного учителя
                var selId = GetSelectedIdFromGrid(dgv, "TEACHER_ID");
                if (selId != null && pictureBox2.Image != null)
                {
                    var blob = DbProcedures.ImageToBlob(pictureBox2.Image);
                    if (blob != null)
                        DbProcedures.UpdateTeacherPhoto(_dbPath, selId.Value, blob);
                }

                LoadTeachersToGrid();
                LoadClassDetailsToGrid();
                LoadCurriculumToGrid();
                LoadFullJournalToGrid();
                MessageBox.Show(this, "Учителя сохранены.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при сохранении учителей:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Сохранение учебного плана (button34)
        private void button34_Click(object sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView4");
                if (!(ctrl is DataGridView dgv)) return;
                if (dgv.DataSource is not DataTable dt) { MessageBox.Show(this, "Нет данных для сохранения.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

                var changes = dt.GetChanges();
                if (changes == null) { MessageBox.Show(this, "Изменений нет.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

                foreach (DataRow row in changes.Rows)
                {
                    if (row.RowState == DataRowState.Modified)
                    {
                        // В GET_CURRICULUM у нас CLASS_NAME, SUBJECT_NAME, TEACHER_FULLNAME, HOURS
                        var className = row["CLASS_NAME"] == DBNull.Value ? null : Convert.ToString(row["CLASS_NAME"]);
                        var subjectName = row["SUBJECT_NAME"] == DBNull.Value ? null : Convert.ToString(row["SUBJECT_NAME"]);
                        var teacherFull = row["TEACHER_FULLNAME"] == DBNull.Value ? null : Convert.ToString(row["TEACHER_FULLNAME"]);
                        int hours = row.Table.Columns.Contains("HOURS") && row["HOURS"] != DBNull.Value ? Convert.ToInt32(row["HOURS"]) : 0;

                        if (string.IsNullOrEmpty(className) || string.IsNullOrEmpty(subjectName))
                            continue;

                        // Найти class_id и subject_id
                        var dtClass = FirebirdDb.ExecuteQuery(_dbPath, "SELECT CLASS_ID FROM CLASSES WHERE CLASS_NAME = @P_CLASS_NAME",
                            new FbParameter("P_CLASS_NAME", FbDbType.VarChar) { Value = className });
                        if (dtClass.Rows.Count == 0) continue;
                        int classId = Convert.ToInt32(dtClass.Rows[0]["CLASS_ID"]);

                        var dtSub = FirebirdDb.ExecuteQuery(_dbPath, "SELECT SUBJECT_ID FROM SUBJECTS WHERE NAME = @P_SUBJECT_NAME",
                            new FbParameter("P_SUBJECT_NAME", FbDbType.VarChar) { Value = subjectName });
                        if (dtSub.Rows.Count == 0) continue;
                        int subjectId = Convert.ToInt32(dtSub.Rows[0]["SUBJECT_ID"]);

                        // Найти teacher_id по полному имени (попробуем несколько вариантов)
                        int teacherId = 0;
                        if (!string.IsNullOrEmpty(teacherFull))
                        {
                            var dtT = FirebirdDb.ExecuteQuery(_dbPath,
                                "SELECT TEACHER_ID FROM TEACHERS WHERE TRIM(LASTNAME || ' ' || FIRSTNAME) = @P_FULL OR TRIM(LASTNAME || ' ' || FIRSTNAME || ' ' || COALESCE(MIDDLENAME,'')) = @P_FULL",
                                new FbParameter("P_FULL", FbDbType.VarChar) { Value = teacherFull });
                            if (dtT.Rows.Count > 0) teacherId = Convert.ToInt32(dtT.Rows[0]["TEACHER_ID"]);
                        }

                        // Если teacherId == 0, можно пропустить или установить NULL (в процедуре требуется валидный TEACHER_ID)
                        if (teacherId == 0)
                        {
                            // попробуем оставить без изменения, либо пропускаем
                            continue;
                        }

                        DbProcedures.UpdateCurriculum(_dbPath, classId, subjectId, teacherId, hours);
                    }
                }

                LoadCurriculumToGrid();
                MessageBox.Show(this, "Учебный план сохранён.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при сохранении учебного плана:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Сохранение достижений (button43)
        private void button43_Click(object sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView5");
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
                        if (row.RowState == DataRowState.Modified)
                        {
                            // В исходной процедуре GET_STUDENT_ACHIEVEMENTS_DETAIL может отсутствовать ACHIEVEMENT_ID.
                            // Попробуем получить ACHIEVEMENT_ID по студенту+title+date.
                            int achievementId = 0;
                            string? lastname = row.Table.Columns.Contains("LASTNAME") && row["LASTNAME"] != DBNull.Value ? Convert.ToString(row["LASTNAME"]) : null;
                            string? firstname = row.Table.Columns.Contains("FIRSTNAME") && row["FIRSTNAME"] != DBNull.Value ? Convert.ToString(row["FIRSTNAME"]) : null;
                            string? title = row.Table.Columns.Contains("TITLE") && row["TITLE"] != DBNull.Value ? Convert.ToString(row["TITLE"]) : null;
                            DateTime? edate = row.Table.Columns.Contains("EVENT_DATE") && row["EVENT_DATE"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["EVENT_DATE"]) : null;

                            if (!string.IsNullOrEmpty(lastname) && !string.IsNullOrEmpty(firstname) && !string.IsNullOrEmpty(title) && edate != null)
                            {
                                var dtId = FirebirdDb.ExecuteQuery(_dbPath,
                                    @"SELECT a.ACHIEVEMENT_ID
                                      FROM ACHIEVEMENTS a
                                      JOIN STUDENTS s ON a.STUDENT_ID = s.STUDENT_ID
                                      WHERE TRIM(s.LASTNAME) = @LN AND TRIM(s.FIRSTNAME) = @FN
                                        AND a.TITLE = @TITLE AND a.EVENT_DATE = @EDATE",
                                    new FbParameter("LN", FbDbType.VarChar) { Value = lastname },
                                    new FbParameter("FN", FbDbType.VarChar) { Value = firstname },
                                    new FbParameter("TITLE", FbDbType.VarChar) { Value = title },
                                    new FbParameter("EDATE", FbDbType.Date) { Value = edate.Value.Date });
                                if (dtId.Rows.Count > 0) achievementId = Convert.ToInt32(dtId.Rows[0]["ACHIEVEMENT_ID"]);
                            }

                            if (achievementId == 0)
                            {
                                // Не получилось найти — пропускаем строку (рекомендуется добавить ACHIEVEMENT_ID в GET_STUDENT_ACHIEVEMENTS_DETAIL)
                                continue;
                            }

                            string titleNew = Convert.ToString(row["TITLE"]) ?? string.Empty;
                            DateTime? eventDate = row.Table.Columns.Contains("EVENT_DATE") && row["EVENT_DATE"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["EVENT_DATE"]) : null;
                            string? level = row.Table.Columns.Contains("LEVEL") && row["LEVEL"] != DBNull.Value ? Convert.ToString(row["LEVEL"]) : null;
                            string? desc = row.Table.Columns.Contains("DESCRIPTION") && row["DESCRIPTION"] != DBNull.Value ? Convert.ToString(row["DESCRIPTION"]) : null;

                            DbProcedures.UpdateAchievement(_dbPath, achievementId, titleNew, eventDate, level, desc);
                            anySaved = true;
                        }
                    }
                }

                // Фото для текущего выбранного достижения (pictureBox3)
                var curRow = dgv.CurrentRow;
                if (curRow != null)
                {
                    var drv = curRow.DataBoundItem as DataRowView;
                    if (drv != null)
                    {
                        int achievementId = 0;
                        // попытка получить ID как в коде выше
                        string? ln = drv.Row.Table.Columns.Contains("LASTNAME") && drv.Row["LASTNAME"] != DBNull.Value ? Convert.ToString(drv["LASTNAME"]) : null;
                        string? fn = drv.Row.Table.Columns.Contains("FIRSTNAME") && drv.Row["FIRSTNAME"] != DBNull.Value ? Convert.ToString(drv["FIRSTNAME"]) : null;
                        string? title = drv.Row.Table.Columns.Contains("TITLE") && drv["TITLE"] != DBNull.Value ? Convert.ToString(drv["TITLE"]) : null;
                        DateTime? ed = drv.Row.Table.Columns.Contains("EVENT_DATE") && drv["EVENT_DATE"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(drv["EVENT_DATE"]) : null;
                        if (!string.IsNullOrEmpty(ln) && !string.IsNullOrEmpty(fn) && !string.IsNullOrEmpty(title) && ed != null)
                        {
                            var dtId = FirebirdDb.ExecuteQuery(_dbPath,
                                @"SELECT a.ACHIEVEMENT_ID
                                  FROM ACHIEVEMENTS a
                                  JOIN STUDENTS s ON a.STUDENT_ID = s.STUDENT_ID
                                  WHERE TRIM(s.LASTNAME) = @LN AND TRIM(s.FIRSTNAME) = @FN
                                    AND a.TITLE = @TITLE AND a.EVENT_DATE = @EDATE",
                                new FbParameter("LN", FbDbType.VarChar) { Value = ln },
                                new FbParameter("FN", FbDbType.VarChar) { Value = fn },
                                new FbParameter("TITLE", FbDbType.VarChar) { Value = title },
                                new FbParameter("EDATE", FbDbType.Date) { Value = ed.Value.Date });
                            if (dtId.Rows.Count > 0) achievementId = Convert.ToInt32(dtId.Rows[0]["ACHIEVEMENT_ID"]);
                        }

                        if (achievementId != 0 && pictureBox3.Image != null)
                        {
                            var blob = DbProcedures.ImageToBlob(pictureBox3.Image);
                            if (blob != null)
                            {
                                DbProcedures.UpdateAchievementPhoto(_dbPath, achievementId, blob);
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

                LoadAchievementsToGrid();
                MessageBox.Show(this, "Достижения сохранены.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при сохранении достижений:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Сохранение классов / назначений (button53)
        private void button53_Click(object sender, EventArgs e)
        {
            try
            {
                var ctrl = FindControlRecursive(this, "dataGridView6");
                if (!(ctrl is DataGridView dgv)) return;
                if (dgv.DataSource is not DataTable dt) { MessageBox.Show(this, "Нет данных для сохранения.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

                var changes = dt.GetChanges();
                if (changes == null) { MessageBox.Show(this, "Изменений нет.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

                foreach (DataRow row in changes.Rows)
                {
                    if (row.RowState == DataRowState.Modified)
                    {
                        // GET_CLASS_DETAILS возвращает CLASS_NAME, TEACHER_FULLNAME, STUDENT_ID, LASTNAME, FIRSTNAME, MIDDLENAME
                        var className = row["CLASS_NAME"] == DBNull.Value ? null : Convert.ToString(row["CLASS_NAME"]);
                        var teacherFull = row.Table.Columns.Contains("TEACHER_FULLNAME") && row["TEACHER_FULLNAME"] != DBNull.Value ? Convert.ToString(row["TEACHER_FULLNAME"]) : null;
                        if (string.IsNullOrEmpty(className)) continue;

                        // найти class_id
                        var dtClass = FirebirdDb.ExecuteQuery(_dbPath, "SELECT CLASS_ID FROM CLASSES WHERE CLASS_NAME = @P_CLASS_NAME",
                            new FbParameter("P_CLASS_NAME", FbDbType.VarChar) { Value = className });
                        if (dtClass.Rows.Count == 0) continue;
                        int classId = Convert.ToInt32(dtClass.Rows[0]["CLASS_ID"]);

                        int? teacherId = null;
                        if (!string.IsNullOrEmpty(teacherFull))
                        {
                            var dtT = FirebirdDb.ExecuteQuery(_dbPath,
                                "SELECT TEACHER_ID FROM TEACHERS WHERE TRIM(LASTNAME || ' ' || FIRSTNAME) = @P_FULL OR TRIM(LASTNAME || ' ' || FIRSTNAME || ' ' || COALESCE(MIDDLENAME,'')) = @P_FULL",
                                new FbParameter("P_FULL", FbDbType.VarChar) { Value = teacherFull });
                            if (dtT.Rows.Count > 0) teacherId = Convert.ToInt32(dtT.Rows[0]["TEACHER_ID"]);
                        }

                        // Обновим класс (назначение классного учителя)
                        var idParam = new FbParameter("P_CLASS_ID", FbDbType.Integer) { Value = classId };
                        var teacherParam = new FbParameter("P_CLASS_TEACHER_ID", FbDbType.Integer) { Value = teacherId ?? (object)DBNull.Value };
                        // Прямой UPDATE
                        FirebirdDb.ExecuteNonQuery(_dbPath, "UPDATE CLASSES SET CLASS_TEACHER_ID = @P_CLASS_TEACHER_ID WHERE CLASS_ID = @P_CLASS_ID", teacherParam, idParam);
                    }
                }

                LoadClassDetailsToGrid();
                LoadCurriculumToGrid();
                MessageBox.Show(this, "Классы сохранены.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при сохранении классов:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --- НИЗКИЕ ЛЕГКИЕ DIALOGS ---
        // Вставьте эти приватные классы внутрь класса Form1.

        private sealed class SimpleInputRow : TableLayoutPanel
        {
            public TextBox TextBox { get; } = new TextBox { Dock = DockStyle.Fill };
            public SimpleInputRow(string label)
            {
                ColumnCount = 2;
                RowCount = 1;
                Dock = DockStyle.Top;
                AutoSize = true;
                Controls.Add(new Label { Text = label, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                Controls.Add(TextBox, 1, 0);
            }
        }

        private sealed class AddStudentDialog : Form
        {
            private readonly string _db;
            private readonly TextBox tbLast = new TextBox();
            private readonly TextBox tbFirst = new TextBox();
            private readonly TextBox tbMiddle = new TextBox();
            private readonly ComboBox cbGender = new ComboBox();
            private readonly DateTimePicker dpBirth = new DateTimePicker { Format = DateTimePickerFormat.Short, ShowUpDown = false };
            private readonly NumericUpDown numYear = new NumericUpDown { Minimum = 1900, Maximum = 2100, Value = (decimal)DateTime.Now.Year };
            private readonly ComboBox cbClass = new ComboBox();
            private readonly TextBox tbPhone = new TextBox();
            private readonly TextBox tbEmail = new TextBox();

            public AddStudentDialog(string dbPath)
            {
                _db = dbPath;
                Text = "Добавить ученика";
                StartPosition = FormStartPosition.CenterParent;
                Width = 420;
                AutoSize = true;
                var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
                tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));

                void AddRow(string label, Control ctrl)
                {
                    var idx = tbl.RowCount++;
                    tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    var lbl = new Label { Text = label, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
                    ctrl.Dock = DockStyle.Fill;
                    tbl.Controls.Add(lbl, 0, idx);
                    tbl.Controls.Add(ctrl, 1, idx);
                }

                cbGender.Items.AddRange(new object[] { "M", "F" });
                cbGender.DropDownStyle = ComboBoxStyle.DropDownList;
                cbGender.SelectedIndex = 0;

                AddRow("Фамилия", tbLast);
                AddRow("Имя", tbFirst);
                AddRow("Отчество", tbMiddle);
                AddRow("Пол (M/F)", cbGender);
                AddRow("Дата рождения", dpBirth);
                AddRow("Год поступления", numYear);
                AddRow("Класс (выбор)", cbClass);
                AddRow("Телефон", tbPhone);
                AddRow("Email", tbEmail);

                // загрузим классы
                try
                {
                    var dt = global::kursovaya.FirebirdDb.ExecuteQuery(_db, "SELECT CLASS_ID, CLASS_NAME FROM CLASSES ORDER BY CLASS_NAME");
                    foreach (DataRow r in dt.Rows)
                    {
                        var classId = Convert.ToInt32(r["CLASS_ID"]);
                        var className = Convert.ToString(r["CLASS_NAME"]) ?? string.Empty;
                        cbClass.Items.Add(new Tuple<int, string>(classId, className));
                    }
                    if (cbClass.Items.Count > 0) cbClass.SelectedIndex = 0;
                    cbClass.DisplayMember = "Item2";
                    cbClass.ValueMember = "Item1";
                }
                catch { /* игнорируем */ }

                var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Right };
                var btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Dock = DockStyle.Left };
                var pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
                pnl.Controls.Add(btnOk); pnl.Controls.Add(btnCancel);

                var outer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown };
                outer.Controls.Add(tbl);
                outer.Controls.Add(pnl);
                Controls.Add(outer);

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                btnOk.Click += (s, e) =>
                {
                    // валидация
                    if (string.IsNullOrWhiteSpace(tbLast.Text) || string.IsNullOrWhiteSpace(tbFirst.Text))
                    {
                        MessageBox.Show(this, "Фамилия и имя обязательны.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        DialogResult = DialogResult.None;
                        return;
                    }
                    if (cbClass.SelectedItem == null)
                    {
                        MessageBox.Show(this, "Выберите класс.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        DialogResult = DialogResult.None;
                        return;
                    }

                    int classId = ((Tuple<int, string>)cbClass.SelectedItem).Item1;
                    char gender = cbGender.SelectedItem?.ToString() == "F" ? 'F' : 'M';
                    DateTime? birth = dpBirth.Value.Date;

                    try
                    {
                        var newId = DbProcedures.AddStudent(_db,
                            tbLast.Text.Trim(), tbFirst.Text.Trim(), string.IsNullOrWhiteSpace(tbMiddle.Text) ? null! : tbMiddle.Text.Trim(),
                            gender, birth, (int)numYear.Value, classId,
                            string.IsNullOrWhiteSpace(tbPhone.Text) ? null! : tbPhone.Text.Trim(),
                            string.IsNullOrWhiteSpace(tbEmail.Text) ? null! : tbEmail.Text.Trim());
                        MessageBox.Show(this, $"Ученик добавлен (ID={newId}).", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Ошибка при добавлении ученика:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DialogResult = DialogResult.None;
                    }
                };
            }
        }

        private sealed class AddTeacherDialog : Form
        {
            private readonly string _db;
            public AddTeacherDialog(string dbPath)
            {
                _db = dbPath;
                Text = "Добавить учителя";
                StartPosition = FormStartPosition.CenterParent;
                Width = 380;
                AutoSize = true;

                var tbLast = new TextBox();
                var tbFirst = new TextBox();
                var tbMiddle = new TextBox();
                var cbGender = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                cbGender.Items.AddRange(new object[] { "M", "F" });
                cbGender.SelectedIndex = 0;
                var tbJob = new TextBox();
                var tbPhone = new TextBox();

                var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
                void AddRow(string lbl, Control c)
                {
                    var idx = tbl.RowCount++;
                    tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    tbl.Controls.Add(new Label { Text = lbl, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft }, 0, idx);
                    c.Dock = DockStyle.Fill;
                    tbl.Controls.Add(c, 1, idx);
                }

                AddRow("Фамилия", tbLast);
                AddRow("Имя", tbFirst);
                AddRow("Отчество", tbMiddle);
                AddRow("Пол (M/F)", cbGender);
                AddRow("Должность", tbJob);
                AddRow("Телефон", tbPhone);

                var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel };
                var pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
                pnl.Controls.Add(btnOk); pnl.Controls.Add(btnCancel);

                var outer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown };
                outer.Controls.Add(tbl); outer.Controls.Add(pnl);
                Controls.Add(outer);

                AcceptButton = btnOk; CancelButton = btnCancel;

                btnOk.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(tbLast.Text) || string.IsNullOrWhiteSpace(tbFirst.Text) || string.IsNullOrWhiteSpace(tbJob.Text))
                    {
                        MessageBox.Show(this, "Фамилия, имя и должность обязательны.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        DialogResult = DialogResult.None;
                        return;
                    }

                    try
                    {
                        char gender = cbGender.SelectedItem?.ToString() == "F" ? 'F' : 'M';
                        var id = DbProcedures.AddTeacher(_db, tbLast.Text.Trim(), tbFirst.Text.Trim(), string.IsNullOrWhiteSpace(tbMiddle.Text) ? null! : tbMiddle.Text.Trim(), gender, tbJob.Text.Trim(), string.IsNullOrWhiteSpace(tbPhone.Text) ? null! : tbPhone.Text.Trim());
                        MessageBox.Show(this, $"Учитель добавлен (ID={id}).", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Ошибка при добавлении учителя:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DialogResult = DialogResult.None;
                    }
                };
            }
        }

        private sealed class AddAchievementDialog : Form
        {
            public AddAchievementDialog(string dbPath)
            {
                Text = "Добавить достижение";
                StartPosition = FormStartPosition.CenterParent;
                AutoSize = true;
                Width = 420;

                var cbStudent = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                try
                {
                    var dt = DbProcedures.GetAllStudents(dbPath);
                    foreach (DataRow r in dt.Rows)
                    {
                        var id = Convert.ToInt32(r["STUDENT_ID"]);
                        var name = Convert.ToString(r["FULLNAME"]) ?? string.Empty;
                        cbStudent.Items.Add(new Tuple<int, string>(id, name));
                    }
                    if (cbStudent.Items.Count > 0) cbStudent.SelectedIndex = 0;
                    cbStudent.DisplayMember = "Item2";
                    cbStudent.ValueMember = "Item1";
                }
                catch { }

                var tbTitle = new TextBox();
                var dpDate = new DateTimePicker { Format = DateTimePickerFormat.Short };
                var tbLevel = new TextBox();
                var tbDesc = new TextBox { Multiline = true, Height = 80, ScrollBars = ScrollBars.Vertical };

                var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
                void AddRow(string lbl, Control c)
                {
                    var idx = tbl.RowCount++;
                    tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                    tbl.Controls.Add(new Label { Text = lbl, AutoSize = true }, 0, idx);
                    c.Dock = DockStyle.Fill;
                    tbl.Controls.Add(c, 1, idx);
                }

                AddRow("Ученик", cbStudent);
                AddRow("Название", tbTitle);
                AddRow("Дата", dpDate);
                AddRow("Уровень", tbLevel);
                AddRow("Описание", tbDesc);

                var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel };
                var pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
                pnl.Controls.Add(btnOk); pnl.Controls.Add(btnCancel);

                var outer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown };
                outer.Controls.Add(tbl); outer.Controls.Add(pnl);
                Controls.Add(outer);

                AcceptButton = btnOk; CancelButton = btnCancel;

                btnOk.Click += (s, e) =>
                {
                    if (cbStudent.SelectedItem == null || string.IsNullOrWhiteSpace(tbTitle.Text))
                    {
                        MessageBox.Show(this, "Выберите ученика и укажите название.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        DialogResult = DialogResult.None; return;
                    }

                    int studentId = ((Tuple<int, string>)cbStudent.SelectedItem).Item1;
                    try
                    {
                        var newId = DbProcedures.AddAchievement(dbPath, studentId, tbTitle.Text.Trim(), dpDate.Value.Date, string.IsNullOrWhiteSpace(tbLevel.Text) ? null! : tbLevel.Text.Trim(), string.IsNullOrWhiteSpace(tbDesc.Text) ? null! : tbDesc.Text.Trim());
                        MessageBox.Show(this, $"Достижение добавлено (ID={newId}).", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Ошибка при добавлении достижения:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DialogResult = DialogResult.None;
                    }
                };
            }
        }

        private sealed class AddGradeDialog : Form
        {
            public AddGradeDialog(string dbPath)
            {
                Text = "Добавить оценку";
                StartPosition = FormStartPosition.CenterParent;
                AutoSize = true;
                Width = 420;

                var cbStudent = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                var cbSubject = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                var numGrade = new NumericUpDown { Minimum = 2, Maximum = 5, Value = 4 };
                var dpDate = new DateTimePicker { Format = DateTimePickerFormat.Short };

                try
                {
                    var dtS = DbProcedures.GetAllStudents(dbPath);
                    foreach (DataRow r in dtS.Rows)
                    {
                        var id = Convert.ToInt32(r["STUDENT_ID"]);
                        var name = Convert.ToString(r["FULLNAME"]) ?? string.Empty;
                        cbStudent.Items.Add(new Tuple<int, string>(id, name));
                    }
                    if (cbStudent.Items.Count > 0)
                    {
                        cbStudent.DisplayMember = "Item2";
                        cbSubject.ValueMember = "Item1";
                        cbStudent.SelectedIndex = 0;
                    }
                }
                catch { }
                try
                {
                    var dtSub = global::kursovaya.FirebirdDb.ExecuteQuery(dbPath, "SELECT SUBJECT_ID, NAME FROM SUBJECTS ORDER BY NAME");
                    foreach (DataRow r in dtSub.Rows)
                    {
                        var id = Convert.ToInt32(r["SUBJECT_ID"]);
                        var name = Convert.ToString(r["NAME"]) ?? string.Empty;
                        cbSubject.Items.Add(new Tuple<int, string>(id, name));
                    }
                    if (cbSubject.Items.Count > 0)
                    {
                        cbSubject.DisplayMember = "Item2";
                        cbSubject.ValueMember = "Item1";
                        cbSubject.SelectedIndex = 0;
                    }
                }
                catch { }

                var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
                void AddRow(string lbl, Control c) { var idx = tbl.RowCount++; tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize)); tbl.Controls.Add(new Label { Text = lbl, AutoSize = true }, 0, idx); c.Dock = DockStyle.Fill; tbl.Controls.Add(c, 1, idx); }

                AddRow("Ученик", cbStudent);
                AddRow("Предмет", cbSubject);
                AddRow("Оценка (2-5)", numGrade);
                AddRow("Дата", dpDate);

                var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel };
                var pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; pnl.Controls.Add(btnOk); pnl.Controls.Add(btnCancel);

                var outer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown };
                outer.Controls.Add(tbl); outer.Controls.Add(pnl); Controls.Add(outer);

                AcceptButton = btnOk; CancelButton = btnCancel;

                btnOk.Click += (s, e) =>
                {
                    if (cbStudent.SelectedItem == null || cbSubject.SelectedItem == null)
                    {
                        MessageBox.Show(this, "Выберите ученика и предмет.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        DialogResult = DialogResult.None; return;
                    }

                    int studentId = ((Tuple<int, string>)cbStudent.SelectedItem).Item1;
                    int subjectId = ((Tuple<int, string>)cbSubject.SelectedItem).Item1;
                    int grade = (int)numGrade.Value;
                    DateTime date = dpDate.Value.Date;

                    try
                    {
                        var newId = DbProcedures.AddGrade(dbPath, studentId, subjectId, grade, date);
                        MessageBox.Show(this, $"Оценка добавлена (ID={newId}).", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Ошибка при добавлении оценки:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DialogResult = DialogResult.None;
                    }
                };
            }
        }

        private sealed class AddAttendanceDialog : Form
        {
            public AddAttendanceDialog(string dbPath)
            {
                Text = "Добавить посещаемость";
                StartPosition = FormStartPosition.CenterParent;
                AutoSize = true;
                Width = 380;

                var cbStudent = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                var cbSubject = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                var cbPresence = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                cbPresence.Items.AddRange(new object[] { "+", "-" });
                cbPresence.SelectedIndex = 0;
                var dpDate = new DateTimePicker { Format = DateTimePickerFormat.Short };

                try
                {
                    var dtS = DbProcedures.GetAllStudents(dbPath);
                    foreach (DataRow r in dtS.Rows)
                    {
                        var id = Convert.ToInt32(r["STUDENT_ID"]);
                        var name = Convert.ToString(r["FULLNAME"]) ?? string.Empty;
                        cbStudent.Items.Add(new Tuple<int, string>(id, name));
                    }
                    if (cbStudent.Items.Count > 0)
                    {
                        cbStudent.DisplayMember = "Item2";
                        cbSubject.ValueMember = "Item1";
                        cbStudent.SelectedIndex = 0;
                    }
                }
                catch { }
                try
                {
                    var dtSub = global::kursovaya.FirebirdDb.ExecuteQuery(dbPath, "SELECT SUBJECT_ID, NAME FROM SUBJECTS ORDER BY NAME");
                    foreach (DataRow r in dtSub.Rows)
                    {
                        var id = Convert.ToInt32(r["SUBJECT_ID"]);
                        var name = Convert.ToString(r["NAME"]) ?? string.Empty;
                        cbSubject.Items.Add(new Tuple<int, string>(id, name));
                    }
                    if (cbSubject.Items.Count > 0)
                    {
                        cbSubject.DisplayMember = "Item2";
                        cbSubject.ValueMember = "Item1";
                        cbSubject.SelectedIndex = 0;
                    }
                }
                catch { }

                var tbl = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
                void AddRow(string lbl, Control c) { var idx = tbl.RowCount++; tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize)); tbl.Controls.Add(new Label { Text = lbl, AutoSize = true }, 0, idx); c.Dock = DockStyle.Fill; tbl.Controls.Add(c, 1, idx); }

                AddRow("Ученик", cbStudent);
                AddRow("Предмет", cbSubject);
                AddRow("Присутствие (+/-)", cbPresence);
                AddRow("Дата", dpDate);

                var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel };
                var pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true }; pnl.Controls.Add(btnOk); pnl.Controls.Add(btnCancel);

                var outer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown };
                outer.Controls.Add(tbl); outer.Controls.Add(pnl); Controls.Add(outer);

                AcceptButton = btnOk; CancelButton = btnCancel;

                btnOk.Click += (s, e) =>
                {
                    if (cbStudent.SelectedItem == null || cbSubject.SelectedItem == null)
                    {
                        MessageBox.Show(this, "Выберите ученика и предмет.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        DialogResult = DialogResult.None; return;
                    }

                    int studentId = ((Tuple<int, string>)cbStudent.SelectedItem).Item1;
                    int subjectId = ((Tuple<int, string>)cbSubject.SelectedItem).Item1;
                    string presence = Convert.ToString(cbPresence.SelectedItem) ?? "+";
                    DateTime date = dpDate.Value.Date;

                    try
                    {
                        var newId = DbProcedures.AddAttendance(dbPath, studentId, subjectId, date, presence[0]);
                        MessageBox.Show(this, $"Запись посещаемости добавлена (ID={newId}).", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Ошибка при добавлении посещаемости:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DialogResult = DialogResult.None;
                    }
                };
            }
        }

     

        // Добавлены вспомогательные методы для получения ID и выбора записи из таблицы
        private int? GetSelectedIdFromGrid(DataGridView dgv, string idColumnName)
        {
            if (dgv == null || dgv.CurrentRow == null) return null;

            // Если привязка к DataRowView
            if (dgv.CurrentRow.DataBoundItem is DataRowView drv)
            {
                var col = drv.Row.Table.Columns
                    .Cast<DataColumn>()
                    .FirstOrDefault(c => string.Equals(c.ColumnName, idColumnName, StringComparison.OrdinalIgnoreCase));
                if (col == null) return null;
                var val = drv.Row[col];
                if (val == DBNull.Value || val == null) return null;
                if (int.TryParse(val.ToString(), out var id)) return id;
                return null;
            }

            // Fallback: взять значение из ячейки по имени колонки DataGridView
            var gridCol = dgv.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => string.Equals(c.Name, idColumnName, StringComparison.OrdinalIgnoreCase));
            if (gridCol != null)
            {
                var cellVal = dgv.CurrentRow.Cells[gridCol.Index]?.Value;
                if (cellVal == null || cellVal == DBNull.Value) return null;
                if (int.TryParse(cellVal.ToString(), out var id)) return id;
            }

            return null;
        }

        private int? ShowPickIdDialog(string title, DataTable dt, string idColumnName, string displayColumnName)
        {
            if (dt == null || dt.Rows.Count == 0)
            {
                MessageBox.Show(this, "Нет доступных записей для выбора.", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            using var dlg = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                Width = 700,
                Height = 420,
                MinimizeBox = false,
                MaximizeBox = false
            };

            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                DataSource = dt,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var btnOk = new Button { Text = "Удалить", DialogResult = DialogResult.OK, Width = 100 };
            var btnCancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 100 };
            btnPanel.Controls.Add(btnOk);
            btnPanel.Controls.Add(btnCancel);

            dlg.Controls.Add(dgv);
            dlg.Controls.Add(btnPanel);
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnCancel;

            // Выбрать первую строку по умолчанию
            if (dgv.Rows.Count > 0) dgv.CurrentCell = dgv.Rows[0].Cells[0];

            if (dlg.ShowDialog(this) != DialogResult.OK) return null;

            if (dgv.CurrentRow == null) return null;

            if (dgv.CurrentRow.DataBoundItem is DataRowView drv)
            {
                var col = drv.Row.Table.Columns.Cast<DataColumn>().FirstOrDefault(c => string.Equals(c.ColumnName, idColumnName, StringComparison.OrdinalIgnoreCase));
                if (col == null) return null;
                var val = drv.Row[col];
                if (val == DBNull.Value || val == null) return null;
                if (int.TryParse(val.ToString(), out var id)) return id;
                return null;
            }
            else
            {
                var col = dgv.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => string.Equals(c.Name, idColumnName, StringComparison.OrdinalIgnoreCase));
                if (col != null)
                {
                    var cell = dgv.CurrentRow.Cells[col.Index]?.Value;
                    if (cell != null && cell != DBNull.Value && int.TryParse(cell.ToString(), out var id)) return id;
                }
            }

            return null;
        }

        // Реализация LoadClassDetailsToGrid (ранее отсутствовала)
        private void LoadClassDetailsToGrid()
        {
            var dt = DbProcedures.CallSelectableProc(_dbPath, "GET_CLASS_DETAILS");

            var ctrl = FindControlRecursive(this, "dataGridView6");
            if (!(ctrl is DataGridView dgv))
                return;

            dgv.AutoGenerateColumns = true;
            dgv.DataSource = dt;

            // Скрыть служебный идентификатор STUDENT_ID
            var idCol = dgv.Columns.Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.Name, "STUDENT_ID", StringComparison.OrdinalIgnoreCase));
            if (idCol != null) idCol.Visible = false;

            // Локальная функция для переименования заголовков (необязательно)
            void SetHeader(string colName, string header)
            {
                var col = dgv.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => string.Equals(c.Name, colName, StringComparison.OrdinalIgnoreCase));
                if (col != null) col.HeaderText = header;
            }

            SetHeader("CLASS_NAME", "Класс");
            SetHeader("TEACHER_FULLNAME", "Классный руководитель");
            SetHeader("LASTNAME", "Фамилия");
            SetHeader("FIRSTNAME", "Имя");
            SetHeader("MIDDLENAME", "Отчество");

            dgv.ReadOnly = true;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgv.MultiSelect = false;

            // Сделать выбор визуально нейтральным (убираем синий фон)
            dgv.EnableHeadersVisualStyles = false;
            var cellBack = dgv.DefaultCellStyle.BackColor;
            if (cellBack == Color.Empty) cellBack = SystemColors.Window;
            dgv.DefaultCellStyle.SelectionBackColor = cellBack;
            dgv.DefaultCellStyle.SelectionForeColor = dgv.DefaultCellStyle.ForeColor;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = dgv.ColumnHeadersDefaultCellStyle.BackColor == Color.Empty ? SystemColors.Control : dgv.ColumnHeadersDefaultCellStyle.BackColor;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = dgv.RowHeadersDefaultCellStyle.BackColor == Color.Empty ? cellBack : dgv.RowHeadersDefaultCellStyle.BackColor;
            dgv.RowHeadersDefaultCellStyle.SelectionForeColor = dgv.RowHeadersDefaultCellStyle.ForeColor;

            // Убрать текущее выделение/рамку фокуса
            dgv.ClearSelection();
            try
            {
                if (dgv.Rows.Count > 0)
                    dgv.CurrentCell = null;
            }
            catch { /* безопасно игнорируем */ }

            // Подписка на изменение выбора — будем дополнительно очищать визуал выделения
            dgv.SelectionChanged -= DataGridView6_SelectionChanged;
            dgv.SelectionChanged += DataGridView6_SelectionChanged;
        }

        private void DataGridView6_SelectionChanged(object? sender, EventArgs e)
        {
            if (!(sender is DataGridView dgv)) return;

            // Гарантируем нейтральный цвет выделения
            var cellBack = dgv.DefaultCellStyle.BackColor;
            if (cellBack == Color.Empty) cellBack = SystemColors.Window;
            dgv.DefaultCellStyle.SelectionBackColor = cellBack;
            dgv.DefaultCellStyle.SelectionForeColor = dgv.DefaultCellStyle.ForeColor;

            // Снимаем подсветку и убираем текущую ячейку, чтобы не было синей рамки
            try
            {
                dgv.ClearSelection();
                if (dgv.CurrentCell != null) dgv.CurrentCell = null;
            }
            catch { /* безопасно игнорируем */ }
        }

        // Обработчик чтения/отображения фото достижения в pictureBox3
        private void DataGridView5_SelectionChanged(object? sender, EventArgs e)
        {
            // Быстрое вспомогательное действие — безопасно удалить предыдущее изображение
            void ClearPicture()
            {
                try
                {
                    var prev = pictureBox3.Image;
                    pictureBox3.Image = null;
                    prev?.Dispose();
                }
                catch { /* ignore */ }
            }

            if (!(sender is DataGridView dgv))
            {
                ClearPicture();
                return;
            }

            if (dgv.CurrentRow == null)
            {
                ClearPicture();
                return;
            }

            var drv = dgv.CurrentRow.DataBoundItem as DataRowView;
            if (drv == null)
            {
                ClearPicture();
                return;
            }

            // Найти колонку PROOF_PHOTO в таблице результатов (без учёта регистра)
            var dtCol = drv.Row.Table.Columns
                .Cast<DataColumn>()
                .FirstOrDefault(c => string.Equals(c.ColumnName, "PROOF_PHOTO", StringComparison.OrdinalIgnoreCase));

            if (dtCol == null)
            {
                ClearPicture();
                return;
            }

            var val = drv.Row[dtCol];
            if (val == DBNull.Value || val == null)
            {
                ClearPicture();
                return;
            }

            byte[]? blob = val as byte[];
            if (blob == null && val is System.Data.Common.DbDataRecord rec && rec[0] is byte[] b)
                blob = b;

            if (blob == null || blob.Length == 0)
            {
                ClearPicture();
                return;
            }

            try
            {
                var img = DbProcedures.BlobToImage(blob);
                if (img == null)
                {
                    ClearPicture();
                    return;
                }

                // Устанавливаем изображение (PictureBox сам подгонит отрисовку, настройки SizeMode уже применены)
                var prev = pictureBox3.Image;
                pictureBox3.Image = img;
                prev?.Dispose();

                pictureBox3.BringToFront();
            }
            catch
            {
                ClearPicture();
            }

            // Подстраховка: сделать цвет выделения нейтральным (уже настраивается в LoadAchievementsToGrid)
            var cellBack2 = dgv.DefaultCellStyle.BackColor;
            if (cellBack2 == Color.Empty) cellBack2 = SystemColors.Window;
            dgv.DefaultCellStyle.SelectionBackColor = cellBack2;
            dgv.DefaultCellStyle.SelectionForeColor = dgv.DefaultCellStyle.ForeColor;
        }
    }
}
