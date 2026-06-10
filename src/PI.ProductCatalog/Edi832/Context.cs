using System;
using System.Collections.Generic;
using PI.ProductCatalog.Edi832;
using PI.ProductCatalog.Models;
using PI.Shared.Models;

namespace PI.ProductCatalog;

public enum Loop
{
    None,
    Main,
    Style,
    Price,
    Subline,
    MAX
}

public class OnSectionChangedArgs : EventArgs
{
    public LIN Previous { get; }
    public LIN Current { get; }

    public OnSectionChangedArgs(LIN previous, LIN current)
    {
        Previous = previous;
        Current = current;
    }
}

public class CatalogParserContext
{
    public IEntityContext EntityContext { get; }
    public string[] CurrTokens { get; set; }
    public int LineNumber { get; set; }
    public string Path { get; }
    public string Line { get; set; }
    public Dictionary<string, ILineParser> Parsers => Sender.GetParsers(CatalogUpdate, Loop);

    public ICatalogFormat Sender { get; }

    public Loop Loop { get; set; } = Loop.None;

    public object[] Values { get; set; }

    public readonly CatalogSyncJob _syncJob;
    public CatalogUpdate CatalogUpdate => _syncJob.Interchange;

    private LIN _ready = null;
    public bool Pop(out LIN ready)
    {
        ready = _ready;
        _ready = null;
        return ready != null;
    }

    private LIN _section = null;
    public LIN Section
    {
        get => _section;
        set
        {
            if (_ready != null)
            {
                throw new ParserException("Queue is not empty");
            }

            _ready = _section;
            _section = value;
        }
    }

    private LINCTP _price;
    public LINCTP Price
    {
        get => _price;
        set
        {
            _price = value;
            Item = null;
        }
    }

    // public BaseUnit BaseUnit { get; set; }

    private SLN _item;

    public SLN Item
    {
        get => _item;
        set
        {
            _item = value;
            ItemPrice = null;
        }
    }

    public SLNCTP ItemPrice { get; set; }

    public UnitOfMeasurement ParseUOM(object value) => Sender.ParseUOM(value);

    public CatalogParserContext(IEntityContext entityContext, CatalogSyncJob syncJob, string path, ICatalogFormat sender)
    {
        EntityContext = entityContext.With(new CatalogUpdateActor(syncJob.Id));
        _syncJob = syncJob;
        Path = path;
        Sender = sender;
    }
}