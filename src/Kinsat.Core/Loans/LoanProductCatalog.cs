using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Kinsat.Sdk.Loans;

namespace Kinsat.Core.Loans;

public sealed class LoanProductCatalog
{
    private readonly ConcurrentDictionary<string, LoanProduct> _products = new();

    public void Register(LoanProduct product) => _products[product.ProductId] = product;

    public LoanProduct Get(string productId) =>
        _products.TryGetValue(productId, out var product)
            ? product
            : throw new KeyNotFoundException($"No loan product '{productId}'.");
}
