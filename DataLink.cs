using System;
using System.Collections.Generic;
using System.Text;

namespace MELPeModem
{

    delegate void ProcessFunction<T>(CNTRL_MSG controlEvent, T inData);
    delegate void RegisterFunction<T>(ProcessFunction<T> dataOutFunction);

    class DataPin
    {
        public string Name;
        public bool IsInputPin = false;
    }

    class InputPin<T> : DataPin where T : struct
    {
        public ProcessFunction<T> ProcessFunc;
        bool AllowMultipleIn;

        public InputPin(string pinName, ProcessFunction<T> processingFunction)
        {
            Name = pinName;
            IsInputPin = true;
            ProcessFunc = processingFunction;
            AllowMultipleIn = false;
        }

        public InputPin(string pinName, ProcessFunction<T> processingFunction, bool allowMultipleIn)
        {
            Name = pinName;
            IsInputPin = true;
            ProcessFunc = processingFunction;
            AllowMultipleIn = allowMultipleIn;
        }

        public void Process(CNTRL_MSG controlEvent, T inData)
        {
            if( ProcessFunc != null) this.ProcessFunc(controlEvent, inData);
        }

        public bool IsMultipleInAllowed { get { return AllowMultipleIn; } }
    }

    class OutputPin<T> : DataPin where T : struct
    {
        RegisterFunction<T> RegisterFunc;
        ProcessFunction<T> ProcessFunc = null;

        public OutputPin(string pinName)
        {
            Name = pinName;
            RegisterFunc = this.DefaultRegister;
        }

        public OutputPin(string pinName, RegisterFunction<T> registrationFunction)
        {
            Name = pinName;
            RegisterFunc = registrationFunction;
        }

        public void Register(ProcessFunction<T> pFunc)
        {
            if(RegisterFunc != null) this.RegisterFunc(pFunc);
        }

        void DefaultRegister(ProcessFunction<T> pFunc)
        {
            ProcessFunc = pFunc;
        }


        public void Process(CNTRL_MSG controlEvent, T inData)
        {
            if( ProcessFunc != null) this.ProcessFunc(controlEvent, inData);
        }

        public void Process(T inData)
        {
            if (ProcessFunc != null) this.ProcessFunc(CNTRL_MSG.DATA_IN, inData);
        }

        public void Process(CNTRL_MSG controlEvent)
        {
            if (ProcessFunc != null) this.ProcessFunc(controlEvent, default(T));
        }
    }

    interface IDataModule
    {
        string get_Name();
        DataPin[] get_InputPins();
        DataPin[] get_OutputPins();
        DataPin[] get_Pins();
        DataPin get_Pin(string pinName);
        void Init();
    }


    abstract class DataProcessingModule : IDataModule
    {
        public string Name;
        List<DataPin> InputPins = new List<DataPin>();
        List<DataPin> OutputPins = new List<DataPin>();
        DataPin this[string name]
        {
            get
            {
                foreach (DataPin dp in InputPins) if (dp.Name == name) return dp;
                foreach (DataPin dp in OutputPins) if (dp.Name == name) return dp;
                return null;
            }
        }

        public DataProcessingModule()
        {
            SetModuleParameters();
        }

        public DataProcessingModule(string moduleName)
        {
            this.Name = moduleName;
            SetModuleParameters();
        }

        public abstract void SetModuleParameters();
        public abstract void Init();


        protected void SetIOParameters(DataPin[] inputOutputPins)
        {
            foreach(DataPin pin in inputOutputPins)
            {
                if(pin.IsInputPin)
                    InputPins.Add(pin);
                else
                    OutputPins.Add(pin);
            }
        }

        protected void SetIOParameters(string moduleName, DataPin[] inputOutputPins)
        {
            Name = moduleName;
            foreach (DataPin pin in inputOutputPins)
            {
                if (pin.IsInputPin)
                    InputPins.Add(pin);
                else
                    OutputPins.Add(pin);
            }
        }

        protected void SetIOParameters(DataPin[] inputs, DataPin[] outputs)
        {
            if (inputs != null) InputPins.AddRange(inputs);
            if (outputs != null) OutputPins.AddRange(outputs);
        }

        protected void SetIOParameters(string moduleName, DataPin[] inputs, DataPin[] outputs)
        {
            Name = moduleName;
            if (inputs != null) InputPins.AddRange(inputs);
            if (outputs != null) OutputPins.AddRange(outputs);
        }


        string IDataModule.get_Name()
        {
            return Name;
        }

        DataPin[] IDataModule.get_InputPins()
        {
            return InputPins.ToArray();
        }

        DataPin[] IDataModule.get_OutputPins()
        {
            return OutputPins.ToArray();
        }

        DataPin[] IDataModule.get_Pins()
        {
            DataPin[] ret = new DataPin[OutputPins.Count + InputPins.Count];
            InputPins.CopyTo(ret, 0);
            OutputPins.CopyTo(ret, InputPins.Count);
            return ret;
        }

        public DataPin get_Pin(string pinName)
        {
            return this[pinName];
        }
    }

    enum CNTRL_MSG
    {
        INIT = 0,
        START,
        QUEUE_CLEAR,
        
        DATA_IN = 0x1000,

        NEW_STATE = 0x3000,
        NEW_SYMBOL,
        NEW_FRAME,
        INTERLEAVER_FRAME,
        SYMBOL_DETECTED,
        SYNC_DETECTED,
        EOM_DETECTED,
        FINISH,
    }

    struct DataPacket<T> where T : struct
    {
        public CNTRL_MSG Control;
        public T Data;
    }

    class DataQueue<T> where T : struct
    {
        Queue<DataPacket<T>> Data = new Queue<DataPacket<T>>();

        public void Init()
        {
            lock (Data)
            {
                Data.Clear();
                Data.TrimExcess();
            }
        }

        public void Clear()
        {
            Init();
        }

        public void PutData(T inData)
        {
            DataPacket<T> dp;
            dp.Control = CNTRL_MSG.DATA_IN; dp.Data = inData;
            lock (Data)
            {
                Data.Enqueue(dp);
            }
        }

        public void Process(CNTRL_MSG inControl, T inData)
        {
            DataPacket<T> dp;
            dp.Control = inControl; dp.Data = inData;
            lock (Data)
            {
                Data.Enqueue(dp);
            }
        }

        public DataPacket<T> GetData()
        {
            DataPacket<T> ret;
            lock (Data)
            {
                ret = Data.Dequeue();
            }
            return ret;
        }

        public int GetData(DataPacket<T>[] outDataArray, int startingIndex)
        {
            int ret;
            lock (Data)
            {
                ret = Data.Count;
                Data.CopyTo(outDataArray, startingIndex);
                Data.Clear();
            }
            return ret;
        }

        public int Count { get { return Data.Count; } }

    }

    class ConnectionEntry<T> where T: struct
    {
        public OutputPin<T> AssociatedOutputPin;
        DataQueue<T> q = new DataQueue<T>();
        List<ProcessFunction<T>> InputProcessFunctions = new List<ProcessFunction<T>>();

        public ConnectionEntry(OutputPin<T> outputPin)
        {
            AssociatedOutputPin = outputPin;
            outputPin.Register(q.Process);
        }

        public void AddInput(InputPin<T> inputPin)
        {
            ProcessFunction<T> pf = inputPin.ProcessFunc;
            if (!InputProcessFunctions.Contains(pf))
            {
                InputProcessFunctions.Add(pf);
            }
        }

        public void Init()
        {
            q.Clear();
        }

        public void Process(CNTRL_MSG inControl, T inData)
        {
            foreach (ProcessFunction<T> f in InputProcessFunctions)
            {
                f(inControl, inData);
            }
        }

        public void Process()
        {
            DataPacket<T> dp;
            while(q.Count > 0)
            {
                dp = q.GetData();
                Process(dp.Control, dp.Data);
            }
        }
        public int Count { get { return q.Count; } }
    }


    class DataConnections
    {
        System.Collections.ArrayList ConnectionsArray = new System.Collections.ArrayList();
        bool ContinueProcessing;

        public void Init() 
        {
            foreach (object conn in ConnectionsArray)
            {
                if (conn is ConnectionEntry<byte>)
                {
                    ((ConnectionEntry<byte>)conn).Init();
                }
                else if (conn is ConnectionEntry<int>)
                {
                    ((ConnectionEntry<int>)conn).Init();
                }
                else if (conn is ConnectionEntry<float>)
                {
                    ((ConnectionEntry<float>)conn).Init();
                }
                else if (conn is ConnectionEntry<IQ>)
                {
                    ((ConnectionEntry<IQ>)conn).Init();
                }
            }
            Process(CNTRL_MSG.QUEUE_CLEAR);
        }

        public void Add<T>(OutputPin<T> pinA, InputPin<T> pinB) where T:struct
        {
            foreach (object conn in ConnectionsArray)
            {
                ConnectionEntry<T> ce = conn as ConnectionEntry<T>;
                if ( (ce != null) && (ce.AssociatedOutputPin == pinA))
                {
                    ce.AddInput(pinB);
                    return;
                }
            }
            // No output pins were found -  create a new queue 
            ConnectionEntry<T> newce = new ConnectionEntry<T>(pinA);
            newce.AddInput(pinB);
            ConnectionsArray.Add(newce);
        }

        public void Add<T>(DataPin pinA, DataPin pinB) where T : struct
        {
            if (pinA.IsInputPin == pinB.IsInputPin)
                return;

            OutputPin<T> pOut = (pinA.IsInputPin ? pinB : pinA) as OutputPin<T>;
            InputPin<T> pIn = (pinA.IsInputPin ? pinA : pinB) as InputPin<T>;

            foreach (object conn in ConnectionsArray)
            {
                ConnectionEntry<T> ce = conn as ConnectionEntry<T>;
                if ((ce != null) && (ce.AssociatedOutputPin == pOut))
                {
                    ce.AddInput(pIn);
                    return;
                }
            }
            // No output pins were found -  create a new queue 
            ConnectionEntry<T> newce = new ConnectionEntry<T>(pOut);
            newce.AddInput(pIn);
            ConnectionsArray.Add(newce);
        }

        public void Process(CNTRL_MSG inControl)
        {
            foreach (object conn in ConnectionsArray)
            {
                if (conn is ConnectionEntry<byte>)
                {
                    ((ConnectionEntry<byte>)conn).Process(inControl, 0);
                }
                else if (conn is ConnectionEntry<int>)
                {
                    ((ConnectionEntry<int>)conn).Process(inControl, 0);
                }
                else if (conn is ConnectionEntry<float>)
                {
                    ((ConnectionEntry<float>)conn).Process(inControl, 0);
                }
                else if (conn is ConnectionEntry<IQ>)
                {
                    ((ConnectionEntry<IQ>)conn).Process(inControl, IQ.ZERO);
                }
            }
        }

        public void Process(bool exitWhenQueuesEmpty)
        {
            ContinueProcessing = true;

            Process(CNTRL_MSG.INIT);
            Init();
            Process(CNTRL_MSG.START);
            while (ContinueProcessing)
            {
                foreach (object conn in ConnectionsArray)
                {
                    if (conn is ConnectionEntry<byte>)
                    {
                        ((ConnectionEntry<byte>)conn).Process();
                    }
                    else if (conn is ConnectionEntry<int>)
                    {
                        ((ConnectionEntry<int>)conn).Process();
                    }
                    else if (conn is ConnectionEntry<float>)
                    {
                        ((ConnectionEntry<float>)conn).Process();
                    }
                    else if (conn is ConnectionEntry<IQ>)
                    {
                        ((ConnectionEntry<IQ>)conn).Process();
                    }
                }

                if (exitWhenQueuesEmpty)
                {
                    int TotalNumberInQueues = 0;
                    foreach (object conn in ConnectionsArray)
                    {
                        if (conn is ConnectionEntry<byte>)
                        {
                            TotalNumberInQueues += ((ConnectionEntry<byte>)conn).Count;
                        }
                        else if (conn is ConnectionEntry<int>)
                        {
                            TotalNumberInQueues += ((ConnectionEntry<int>)conn).Count;
                        }
                        else if (conn is ConnectionEntry<float>)
                        {
                            TotalNumberInQueues += ((ConnectionEntry<float>)conn).Count;
                        }
                        else if (conn is ConnectionEntry<IQ>)
                        {
                            TotalNumberInQueues += ((ConnectionEntry<IQ>)conn).Count;
                        }
                    }
                    // Now we have the total number of data elements in all queues
                    if (TotalNumberInQueues == 0)
                    {
                        ContinueProcessing = false;
                    }
                }
            }
        }

        public void Stop()
        {
            ContinueProcessing = false;
        }
    }
}
