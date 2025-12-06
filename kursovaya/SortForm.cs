using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace kursovaya
{
    public partial class SortForm : Form
    {
        private readonly string _dbPath;
        private readonly DataGridView _targetGrid;

        // Списки метаданных, соответствующие индексам элементов в clbRelations и пр.
        private RelationInfo[] _allRelations = Array.Empty<RelationInfo>();

        private sealed record DisplayItem(string Name, string Display)
        {
            public override string ToString() => Display ?? Name;
        }

        private static readonly Dictionary<string, string> TableDisplay = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ACHIEVEMENTS", "Достижения" },
            { "STUDENTS", "Ученики" },
            { "ATTENDANCE", "Посещаемость" },
            { "SUBJECTS", "Предметы" },
            { "CURRICULUM", "Учебный план" },
            { "CLASSES", "Классы" },
            { "TEACHERS", "Учителя" },
            { "FINAL_GRADES", "Итоговые оценки" },
            { "GRADES", "Оценки" }
        };

        private static readonly Dictionary<string, Dictionary<string, string>> ColumnDisplay = new(StringComparer.OrdinalIgnoreCase)
        {
            ["STUDENTS"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["STUDENT_ID"] = "ID ученика",
                ["LASTNAME"] = "Фамилия",
                ["FIRSTNAME"] = "Имя",
                ["MIDDLENAME"] = "Отчество",
                ["GENDER"] = "Пол",
                ["BIRTHDATE"] = "Дата рождения",
                ["YEAR_ADMISSION"] = "Год поступления",
                ["CLASS_ID"] = "ID класса",
                ["PHONE"] = "Телефон",
                ["EMAIL"] = "Email",
                ["PHOTO"] = "Фото"
            },
            ["TEACHERS"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["TEACHER_ID"] = "ID учителя",
                ["LASTNAME"] = "Фамилия",
                ["FIRSTNAME"] = "Имя",
                ["MIDDLENAME"] = "Отчество",
                ["GENDER"] = "Пол",
                ["JOB_TITLE"] = "Должность",
                ["PHONE"] = "Телефон",
                ["PHOTO"] = "Фото"
            },
            ["ATTENDANCE"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ATTENDANCE_ID"] = "ID посещения",
                ["STUDENT_ID"] = "ID ученика",
                ["SUBJECT_ID"] = "ID предмета",
                ["LESSON_DATE"] = "Дата занятия",
                ["PRESENCE"] = "Присутствие"
            },
            ["ACHIEVEMENTS"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ACHIEVEMENT_ID"] = "ID достижения",
                ["STUDENT_ID"] = "ID ученика",
                ["TITLE"] = "Название",
                ["EVENT_DATE"] = "Дата события",
                ["LEVEL"] = "Уровень",
                ["DESCRIPTION"] = "Описание",
                ["PROOF_PHOTO"] = "Фото"
            },
            ["SUBJECTS"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SUBJECT_ID"] = "ID предмета",
                ["NAME"] = "Название предмета"
            },
            ["CLASSES"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["CLASS_ID"] = "ID класса",
                ["CLASS_NAME"] = "Класс",
                ["CLASS_TEACHER_ID"] = "Классный руководитель (ID)"
            },
            // при необходимости добавьте остальные таблицы/колонки
        };

        private static string GetTableDisplay(string table) =>
            string.IsNullOrWhiteSpace(table) ? table : (TableDisplay.TryGetValue(table, out var d) ? d : table);

        private static string GetColumnDisplay(string table, string column)
        {
            if (string.IsNullOrWhiteSpace(column)) return column;
            if (!string.IsNullOrWhiteSpace(table) && ColumnDisplay.TryGetValue(table, out var map) && map.TryGetValue(column, out var d))
                return d;
            return column;
        }

        public SortForm(string dbPath, DataGridView targetGrid)
        {
            if (string.IsNullOrWhiteSpace(dbPath)) throw new ArgumentNullException(nameof(dbPath));
            if (targetGrid == null) throw new ArgumentNullException(nameof(targetGrid));

            InitializeComponent();

            _dbPath = dbPath;
            _targetGrid = targetGrid;

            // Настройка контролов, если нужно
            // Показать понятные метки пользователю, а реально для SQL хранить "ASC"/"DESC"
            var sortOptions = new[]
            {
                new SortOption("По возрастанию (ASC)", "ASC"),
                new SortOption("По убыванию (DESC)", "DESC")
            };
            cbSortDir.DataSource = sortOptions;
            cbSortDir.DisplayMember = nameof(SortOption.Display);
            cbSortDir.ValueMember = nameof(SortOption.Sql);
            cbSortDir.SelectedIndex = 0;

            Load += SortForm_Load;
        }

        private void SortForm_Load(object? sender, EventArgs e)
        {
            try
            {
                // Загрузить список пользовательских таблиц
                var tables = GetUserTables(_dbPath);
                clbTables.Items.Clear();
                foreach (var t in tables)
                {
                    clbTables.Items.Add(new DisplayItem(t, GetTableDisplay(t)), false);
                }

                // Заполнить базовую таблицу пусто — будет доступна после выбора таблиц
                cbBaseTable.Items.Clear();

                // Очистить другие элементы
                clbRelations.Items.Clear();
                clbColumns.Items.Clear();
                cbSortColumn.Items.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при загрузке списка таблиц:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnLoadRelations_Click(object sender, EventArgs e)
        {
            var selectedTables = clbTables.CheckedItems.Cast<DisplayItem>().Select(di => di.Name).ToArray();
            if (selectedTables.Length == 0)
            {
                MessageBox.Show(this, "Отметьте хотя бы одну таблицу.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _allRelations = GetForeignKeyRelations(_dbPath);
                // Оставляем только отношения между избранными таблицами
                var relevant = _allRelations.Where(r => selectedTables.Contains(r.FkTable, StringComparer.OrdinalIgnoreCase)
                                                      && selectedTables.Contains(r.PkTable, StringComparer.OrdinalIgnoreCase))
                                            .ToArray();

                clbRelations.Items.Clear();
                foreach (var r in relevant)
                {
                    var desc = $"{r.Name}: {GetTableDisplay(r.FkTable)} -> {GetTableDisplay(r.PkTable)} ({string.Join(", ", r.Pairs.Select(p => $"{GetColumnDisplay(r.FkTable, p.Fk)} = {GetColumnDisplay(r.PkTable, p.Pk)}"))})";
                    clbRelations.Items.Add(desc, false);
                }

                if (relevant.Length == 0)
                {
                    MessageBox.Show(this, "Связей (FK) между отмеченными таблицами не найдено.", "Информация", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                // Заполняем cbBaseTable DisplayItem'ами
                cbBaseTable.Items.Clear();
                foreach (var t in selectedTables)
                    cbBaseTable.Items.Add(new DisplayItem(t, GetTableDisplay(t)));
                if (cbBaseTable.Items.Count > 0) cbBaseTable.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при загрузке связей:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnLoadColumns_Click(object sender, EventArgs e)
        {
            var selectedTables = clbTables.CheckedItems.Cast<DisplayItem>().Select(di => di.Name).ToArray();
            if (selectedTables.Length == 0)
            {
                MessageBox.Show(this, "Отметьте хотя бы одну таблицу перед загрузкой колонок.", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            clbColumns.Items.Clear();
            try
            {
                foreach (var t in selectedTables)
                {
                    var cols = GetTableColumns(_dbPath, t);
                    foreach (var c in cols)
                    {
                        var name = $"{t}.{c}";
                        var display = $"{GetTableDisplay(t)}.{GetColumnDisplay(t, c)}";
                        clbColumns.Items.Add(new DisplayItem(name, display), false);
                    }
                }

                // Заполняем cbSortColumn совпадающими DisplayItem (чтоб интерфейс показывал русские подписи)
                cbSortColumn.Items.Clear();
                cbSortColumn.Items.AddRange(clbColumns.Items.Cast<object>().ToArray());
                if (cbSortColumn.Items.Count > 0) cbSortColumn.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка при загрузке колонок:\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            // Собираем выборы
            // В начале btnOk_Click — чтение выбранных таблиц:
            var chosenTables = clbTables.CheckedItems.Cast<DisplayItem>().Select(di => di.Name).ToArray();
            if (chosenTables.Length == 0)
            {
                MessageBox.Show(this, "Не выбраны таблицы.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Выбранные связи — соответствуют видимым элементам в clbRelations
            var relationItems = clbRelations.Items.Cast<object>().ToArray();
            var chosenRelations = new List<RelationInfo>();
            for (int i = 0; i < relationItems.Length; i++)
            {
                if (clbRelations.GetItemChecked(i))
                {
                    // Нам нужно найти соответствующий RelationInfo из _allRelations
                    // В clbRelations мы добавляли только релевантные отношения в порядке фильтрации.
                    // Для сопоставления снова вычислим relevant и берём по индексу.
                }
            }

            // Чтобы сопоставлять, пересоздадим релевантный набор в том же порядке, что и при btnLoadRelations_Click
            var relevant = _allRelations.Where(r => chosenTables.Contains(r.FkTable, StringComparer.OrdinalIgnoreCase)
                                                  && chosenTables.Contains(r.PkTable, StringComparer.OrdinalIgnoreCase))
                                        .ToArray();
            for (int i = 0; i < relevant.Length; i++)
            {
                if (i < clbRelations.Items.Count && clbRelations.GetItemChecked(i))
                    chosenRelations.Add(relevant[i]);
            }

            // Колонки для вывода
            // var selectedCols = clbColumns.CheckedItems.Cast<string>().ToArray();
            var selectedCols = clbColumns.CheckedItems.Cast<DisplayItem>().Select(di => di.Name).ToArray();
            if (selectedCols.Length == 0)
            {
                MessageBox.Show(this, "Не выбраны колонки для вывода.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Базовая таблица
            // var baseTable = cbBaseTable.SelectedItem as string ?? chosenTables[0];
            var baseTable = (cbBaseTable.SelectedItem as DisplayItem)?.Name ?? chosenTables[0];

            // Сортировка
            // var sortColumn = cbSortColumn.SelectedItem as string ?? string.Empty;
            // берем реальное значение (SQL) из SelectedValue — в DataSource хранится "ASC"/"DESC"
            // var sortDir = cbSortDir.SelectedValue as string ?? "ASC";
            var sortColumn = (cbSortColumn.SelectedItem as DisplayItem)?.Name ?? string.Empty;
            var sortDir = cbSortDir.SelectedValue as string ?? "ASC";

            string sql = string.Empty;
            try
            {
                sql = BuildSql(chosenTables, baseTable, chosenRelations, selectedCols);
                if (!string.IsNullOrEmpty(sortColumn))
                {
                    sql += $" ORDER BY {QuoteIdentifier(sortColumn.Contains('.') ? sortColumn.Split('.', 2)[1] : sortColumn)} {sortDir}";
                }

                var dt = global::kursovaya.FirebirdDb.ExecuteQuery(_dbPath, sql);

                // Очищаем старые колонки/источник — гарантируем, что в гриде останутся только колонки из нового результата
                _targetGrid.DataSource = null;
                _targetGrid.Columns.Clear();

                _targetGrid.AutoGenerateColumns = true;
                _targetGrid.DataSource = dt;

                // Проставляем русские заголовки по порядку выбранных колонок (selectedCols)
                try
                {
                    for (int i = 0; i < selectedCols.Length && i < _targetGrid.Columns.Count; i++)
                    {
                        var sc = selectedCols[i] ?? string.Empty;
                        string table = null, col = sc;
                        if (sc.Contains('.'))
                        {
                            var parts = sc.Split(new[] { '.' }, 2);
                            table = parts[0];
                            col = parts[1];
                        }

                        // Если имя в кавычках — убрать их
                        if (col.Length >= 2 && col.StartsWith("\"") && col.EndsWith("\""))
                            col = col.Substring(1, col.Length - 2);

                        var header = GetColumnDisplay(table, col);
                        // Если есть префикс таблицы в отображении — можно показать "Таблица.Колонка"
                        _targetGrid.Columns[i].HeaderText = header;
                    }
                }
                catch
                {
                    // безопасно игнорируем возможные ошибки отображения заголовков
                }

                _targetGrid.ClearSelection();

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Ошибка выполнения запроса:\n{ex.Message}\n\nSQL:\n{sql}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        // ---- Метаданные / построение SQL ----

        // Замените реализацию метода GetUserTables на эту — она безопасно работает с возможным null/DBNULL
        private static string[] GetUserTables(string dbPath)
        {
            const string sql = @"
SELECT TRIM(r.RDB$RELATION_NAME) AS RELATION_NAME
FROM RDB$RELATIONS r
WHERE (r.RDB$SYSTEM_FLAG = 0 OR r.RDB$SYSTEM_FLAG IS NULL)
  AND r.RDB$VIEW_BLR IS NULL
ORDER BY RELATION_NAME";
            var dt = global::kursovaya.FirebirdDb.ExecuteQuery(dbPath, sql);
            if (dt == null) return Array.Empty<string>();

            return dt.Rows
                     .Cast<DataRow>()
                     .Select(r => r.Field<string>("RELATION_NAME")?.Trim())
                     .Where(s => !string.IsNullOrEmpty(s))
                     .ToArray();
        }

        private static string[] GetTableColumns(string dbPath, string table)
        {
            try
            {
                var dt = global::kursovaya.FirebirdDb.ExecuteQuery(dbPath, $"SELECT * FROM \"{table}\" WHERE 1=0");
                return dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
            }
            catch
            {
                try
                {
                    var dt = global::kursovaya.FirebirdDb.ExecuteQuery(dbPath, $"SELECT * FROM {table} WHERE 1=0");
                    return dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }
        }

        // Замените реализацию метода GetForeignKeyRelations на эту — использует безопасные Field<T> и проверяет пустые значения
        private static RelationInfo[] GetForeignKeyRelations(string dbPath)
        {
            const string metaSql = @"
SELECT
  TRIM(rc_f.RDB$CONSTRAINT_NAME) AS FK_NAME,
  TRIM(rc_f.RDB$RELATION_NAME) AS FK_TABLE,
  TRIM(rc_p.RDB$RELATION_NAME) AS PK_TABLE,
  TRIM(is_f.RDB$FIELD_NAME) AS FK_COLUMN,
  TRIM(is_p.RDB$FIELD_NAME) AS PK_COLUMN,
  is_f.RDB$FIELD_POSITION AS FK_POS,
  is_p.RDB$FIELD_POSITION AS PK_POS
FROM RDB$REF_CONSTRAINTS ref
JOIN RDB$RELATION_CONSTRAINTS rc_f ON rc_f.RDB$CONSTRAINT_NAME = ref.RDB$CONSTRAINT_NAME
JOIN RDB$RELATION_CONSTRAINTS rc_p ON rc_p.RDB$CONSTRAINT_NAME = ref.RDB$CONST_NAME_UQ
JOIN RDB$INDEX_SEGMENTS is_f ON is_f.RDB$INDEX_NAME = rc_f.RDB$INDEX_NAME
JOIN RDB$INDEX_SEGMENTS is_p ON is_p.RDB$INDEX_NAME = rc_p.RDB$INDEX_NAME
ORDER BY FK_NAME, is_f.RDB$FIELD_POSITION";
            var dt = global::kursovaya.FirebirdDb.ExecuteQuery(dbPath, metaSql);
            if (dt == null) return Array.Empty<RelationInfo>();

            var groups = dt.Rows
                           .Cast<DataRow>()
                           .GroupBy(r => (r["FK_NAME"] == DBNull.Value ? string.Empty : r["FK_NAME"].ToString().Trim()), StringComparer.OrdinalIgnoreCase);

            var result = new List<RelationInfo>();
            foreach (var g in groups)
            {
                // безопасно получить позиции как int (обрабатываем Int16/Int32/DBNull)
                var rows = g.OrderBy(r =>
                {
                    var posObj = r["FK_POS"];
                    return posObj == DBNull.Value ? 0 : Convert.ToInt32(posObj);
                }).ToArray();

                if (rows.Length == 0) continue;

                var fkTable = rows[0]["FK_TABLE"] == DBNull.Value ? string.Empty : rows[0]["FK_TABLE"].ToString().Trim();
                var pkTable = rows[0]["PK_TABLE"] == DBNull.Value ? string.Empty : rows[0]["PK_TABLE"].ToString().Trim();
                if (string.IsNullOrEmpty(fkTable) || string.IsNullOrEmpty(pkTable))
                    continue;

                var pairs = rows.Select(r => (
                    Fk: r["FK_COLUMN"] == DBNull.Value ? string.Empty : r["FK_COLUMN"].ToString().Trim(),
                    Pk: r["PK_COLUMN"] == DBNull.Value ? string.Empty : r["PK_COLUMN"].ToString().Trim()
                )).ToArray();

                result.Add(new RelationInfo(g.Key, fkTable, pkTable, pairs));
            }

            return result.ToArray();
        }

        // Замените старую реализацию BuildSql этой — она даёт алиасы для выбранных колонок
        private static string BuildSql(string[] chosenTables, string baseTable, IEnumerable<RelationInfo> chosenRelations, string[] selectedCols)
        {
            var relations = chosenRelations.ToList();
            var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseTable };
            var joins = new List<string>();

            bool progress;
            do
            {
                progress = false;
                foreach (var rel in relations.ToArray())
                {
                    if (included.Contains(rel.FkTable) && !included.Contains(rel.PkTable))
                    {
                        joins.Add(BuildJoin(rel.FkTable, rel.PkTable, rel.Pairs, isFkLeft: true));
                        included.Add(rel.PkTable);
                        relations.Remove(rel);
                        progress = true;
                    }
                    else if (included.Contains(rel.PkTable) && !included.Contains(rel.FkTable))
                    {
                        joins.Add(BuildJoin(rel.PkTable, rel.FkTable, rel.Pairs, isFkLeft: false));
                        included.Add(rel.FkTable);
                        relations.Remove(rel);
                        progress = true;
                    }
                }
            } while (progress);

            var notIncluded = chosenTables.Where(t => !included.Contains(t)).ToArray();
            var from = QuoteIdentifier(baseTable);

            // Строим селекты с алиасами (чтобы заголовки были простые и уникальные)
            var aliasCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var selectExprs = new List<string>();
            foreach (var sc in selectedCols)
            {
                var expr = QuoteIdentifier(sc);
                var baseAlias = GetColumnAlias(sc);
                if (aliasCounts.TryGetValue(baseAlias, out var cnt))
                {
                    cnt++;
                    aliasCounts[baseAlias] = cnt;
                    baseAlias = $"{baseAlias}_{cnt}";
                }
                else
                {
                    aliasCounts[baseAlias] = 0;
                }

                selectExprs.Add($"{expr} AS {QuoteIdentifier(baseAlias)}");
            }

            var sql = "SELECT " + string.Join(", ", selectExprs) + " FROM " + from;

            foreach (var j in joins) sql += " " + j;
            foreach (var t in notIncluded) sql += " CROSS JOIN " + QuoteIdentifier(t);

            return sql;
        }

        // Вспомогательная функция — извлекает безопасный alias из "Table.Column" или "Column"
        private static string GetColumnAlias(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "COL";

            var s = source.Trim();

            // если "Table.Column" — берём часть после точки
            if (s.Contains('.'))
                s = s.Split(new[] { '.' }, 2)[1];

            s = s.Trim();

            // убираем кавычки вокруг, если есть
            if (s.Length >= 2 && s.StartsWith("\"") && s.EndsWith("\""))
                s = s.Substring(1, s.Length - 2);

            // оставляем только буквы/цифры/_
            var cleaned = new string(s.Select(ch => (char.IsLetterOrDigit(ch) || ch == '_') ? ch : '_').ToArray());
            if (string.IsNullOrEmpty(cleaned))
                cleaned = "COL";

            return cleaned;
        }

        // Исправление: заменить LEFT JOIN на INNER JOIN — чтобы строки базовой таблицы без соответствий в связанных таблицах НЕ попадали в результат
        private static string BuildJoin(string leftTable, string rightTable, (string Fk, string Pk)[] pairs, bool isFkLeft)
        {
            var join = $" INNER JOIN {QuoteIdentifier(rightTable)} ON ";
            var clauses = new List<string>();
            foreach (var p in pairs)
            {
                string leftCol = isFkLeft ? $"{QuoteIdentifier(leftTable)}.{QuoteIdentifier(p.Fk)}" : $"{QuoteIdentifier(leftTable)}.{QuoteIdentifier(p.Pk)}";
                string rightCol = isFkLeft ? $"{QuoteIdentifier(rightTable)}.{QuoteIdentifier(p.Pk)}" : $"{QuoteIdentifier(rightTable)}.{QuoteIdentifier(p.Fk)}";
                clauses.Add($"{leftCol} = {rightCol}");
            }
            join += string.Join(" AND ", clauses);
            return join;
        }

        private static string QuoteIdentifier(string name)
        {
            // name like "Table.Column" or "Table.Column AS ..." — handle basic case
            if (string.IsNullOrEmpty(name)) return name;
            if (name.Contains(' '))
                return name; // do not quote aliases or expressions
            if (name.Contains('.'))
            {
                var parts = name.Split(new[] { '.' }, 2);
                // удваиваем возможные двойные кавычки внутри частей
                var left = parts[0].Replace("\"", "\"\"");
                var right = parts[1].Replace("\"", "\"\"");
                return $"\"{left}\".\"{right}\"";
            }
            // удваиваем возможные кавычки
            return $"\"{name.Replace("\"", "\"\"")}\"";
        }

        private sealed record RelationInfo(string Name, string FkTable, string PkTable, (string Fk, string Pk)[] Pairs);

        // Небольшой тип для представления варианта сортировки в UI
        private sealed record SortOption(string Display, string Sql);

        // Добавлены пустые обработчики событий, на которые ссылается SortForm.Designer.cs.
        // Вставьте этот блок внутрь класса `SortForm` (например после существующих методов).
        private void lblTables_Click(object? sender, EventArgs e)
        {
            // пустая заглушка — клик по метке не требует действия
        }

        private void clbTables_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // пустая заглушка — можно использовать для динамического обновления состояния кнопок
        }

        private void lblRelations_Click(object? sender, EventArgs e)
        {
            // пустая заглушка
        }

        private void clbRelations_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // пустая заглушка
        }

        private void lblBaseTable_Click(object? sender, EventArgs e)
        {
            // пустая заглушка
        }
                
        private void cbBaseTable_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // пустая заглушка — можно здесь обновить подсказки/валидацию базовой таблицы
        }

        private void clbColumns_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // При изменении списка колонок можно обновлять список сортировки — реализуем простую синхронизацию:
            try
            {
                cbSortColumn.Items.Clear();
                foreach (var item in clbColumns.CheckedItems)
                {
                    cbSortColumn.Items.Add(item);
                }

                // если ничего не выбрано для сортировки — показать все доступные
                if (cbSortColumn.Items.Count == 0)
                {
                    foreach (var item in clbColumns.Items)
                        cbSortColumn.Items.Add(item);
                }

                if (cbSortColumn.Items.Count > 0 && cbSortColumn.SelectedIndex < 0)
                    cbSortColumn.SelectedIndex = 0;
            }
            catch
            {
                // безопасно игнорируем ошибки синхронизации
            }
        }

        private void lblSort_Click(object? sender, EventArgs e)
        {
            // пустая заглушка
        }

        private void cbSortColumn_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // пустая заглушка — можно при выборе колонки подгружать тип/формат
        }

        private void cbSortDir_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // пустая заглушка
        }
    }
}
