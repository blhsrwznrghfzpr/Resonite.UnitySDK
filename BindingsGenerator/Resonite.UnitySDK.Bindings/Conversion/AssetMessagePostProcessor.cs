using System;

public abstract class AssetMessagePostProcessor
{
    public abstract void Process(ResoniteLink.Message message);

    public abstract bool HasChanged();
}

public class AssetMessageDelegatePostProcessor : AssetMessagePostProcessor
{
    public readonly Action<ResoniteLink.Message> DelegateProcess;

    public AssetMessageDelegatePostProcessor(Action<ResoniteLink.Message> delegateProcess)
    {
        DelegateProcess = delegateProcess;
    }

    public override void Process(ResoniteLink.Message message)
    {
        DelegateProcess.Invoke(message);
    }

    public override bool HasChanged() => false;
}
