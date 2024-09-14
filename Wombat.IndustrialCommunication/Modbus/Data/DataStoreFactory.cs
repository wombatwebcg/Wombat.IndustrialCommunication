namespace Wombat.IndustrialCommunication.Modbus.Data
{
    public static class DataStoreFactory
    {
        public static DataStore CreateDefaultDataStore()
        {
            return CreateDefaultDataStore(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
        }

        public static DataStore CreateDefaultDataStore(ushort coilsCount, ushort inputsCount, ushort holdingRegistersCount, ushort inputRegistersCount)
        {
            var coils = new bool[coilsCount];
            var inputs = new bool[inputsCount];
            var holdingRegs = new ushort[holdingRegistersCount];
            var inputRegs = new ushort[inputRegistersCount];

            return new DataStore(coils, inputs, holdingRegs, inputRegs);
        }

        internal static DataStore CreateTestDataStore()
        {
            DataStore dataStore = new DataStore();

            for (int i = 1; i < 3000; i++)
            {
                bool value = i % 2 > 0;
                dataStore.CoilDiscretes.Add(value);
                dataStore.InputDiscretes.Add(!value);
                dataStore.HoldingRegisters.Add((ushort)i);
                dataStore.InputRegisters.Add((ushort)(i * 10));
            }

            return dataStore;
        }
    }
}
