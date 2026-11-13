using System;
using System.Collections.Generic;
using System.Text;
using LastRide.Core;

namespace LastRide.Fire
{
    /// <summary>Запись инвентаря для сохранения.</summary>
    [Serializable]
    public struct FuelStack
    {
        public string id;
        public int count;
    }

    /// <summary>
    /// Инвентарь топлива (ТЗ 4.1): счётчики по типам, без лимита. Один на игру (<c>GameManager.Inventory</c>).
    /// Изменения публикуются событием <see cref="InventoryChanged"/>; читать счётчики можно напрямую.
    /// </summary>
    public sealed class Inventory
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public int Count(FuelType fuel) => fuel != null && _counts.TryGetValue(fuel.id, out int n) ? n : 0;

        public int Total
        {
            get { int t = 0; foreach (var v in _counts.Values) t += v; return t; }
        }

        public void Add(FuelType fuel, int amount = 1)
        {
            if (fuel == null || amount <= 0) return;
            Set(fuel, Count(fuel) + amount, amount);
        }

        /// <summary>Забрать предметы, если их хватает. Возвращает false, если не хватает.</summary>
        public bool TryTake(FuelType fuel, int amount = 1)
        {
            if (fuel == null || amount <= 0 || Count(fuel) < amount) return false;
            Set(fuel, Count(fuel) - amount, -amount);
            return true;
        }

        public void Clear()
        {
            var ids = new List<string>(_counts.Keys);
            _counts.Clear();
            var cfg = GameConfig.Load();
            foreach (var id in ids)
            {
                var fuel = cfg != null ? cfg.GetFuel(id) : null;
                if (fuel != null) EventBus<InventoryChanged>.Publish(new InventoryChanged { Fuel = fuel, Count = 0, Delta = 0 });
            }
        }

        public FuelStack[] ToArray()
        {
            var list = new List<FuelStack>();
            foreach (var kv in _counts) if (kv.Value > 0) list.Add(new FuelStack { id = kv.Key, count = kv.Value });
            return list.ToArray();
        }

        /// <summary>Заменить содержимое (загрузка чекпоинта).</summary>
        public void Restore(FuelStack[] stacks)
        {
            Clear();
            if (stacks == null) return;
            var cfg = GameConfig.Load();
            foreach (var s in stacks)
            {
                var fuel = cfg != null ? cfg.GetFuel(s.id) : null;
                if (fuel != null) Add(fuel, s.count);
            }
        }

        public override string ToString()
        {
            var cfg = GameConfig.Load();
            var sb = new StringBuilder();
            if (cfg != null)
                foreach (var f in cfg.fuelTypes)
                    if (f != null) sb.Append(f.displayName).Append(' ').Append(Count(f)).Append("  ");
            return sb.ToString().TrimEnd();
        }

        private void Set(FuelType fuel, int value, int delta)
        {
            _counts[fuel.id] = value;
            EventBus<InventoryChanged>.Publish(new InventoryChanged { Fuel = fuel, Count = value, Delta = delta });
        }
    }
}
