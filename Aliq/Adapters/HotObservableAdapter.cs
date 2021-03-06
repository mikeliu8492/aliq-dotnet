﻿using Aliq.Bags;
using Aliq.Linq;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Aliq.Adapters
{
    public sealed class HotObservableAdapter
    {
        public void SetInput<T>(ExternalInput<T> bag, IObservable<T> input)
        {
            var subject = (Subject<T>)Get(bag);
            StartSubject.Subscribe(_ => input.Subscribe(subject));
        }

        public IObservable<T> Get<T>(Bag<T> bag)
            => Map.GetOrCreate(
                bag,
                () => bag.Accept(new CreateVisitor<T>(this)));

        public void Start()
            => Observable
                .Return(new Void())
                .Subscribe(StartSubject);

        private Subject<Void> StartSubject { get; }
            = new Subject<Void>();

        private sealed class CreateVisitor<T> : Bag<T>.IVisitor<IObservable<T>>
        {
            public CreateVisitor(HotObservableAdapter adapter)
            {
                Adapter = adapter;
            }

            public IObservable<T> Visit<I>(SelectMany<T, I> selectMany)
                => Adapter.Get(selectMany.Input).SelectMany(selectMany.Func);

            public IObservable<T> Visit(Merge<T> merge)
            {
                var a = Adapter.Get(merge.InputA);
                var b = Adapter.Get(merge.InputB);
                return a.Merge(b);
            }

            public IObservable<T> Visit(ExternalInput<T> externalInput)
                => new Subject<T>();

            public IObservable<T> Visit(Const<T> const_)
                => Adapter.StartSubject.Select(_ => const_.Value);

            public IObservable<T> Visit<I>(GroupBy<T, I> groupBy)
                => Adapter
                    .Get(groupBy.Input)
                    .GroupBy(x => x.Key, x => x.Value)
                    .SelectMany(g => g.Aggregate(groupBy.Reduce).Select(v => (g.Key, v)))
                    .SelectMany(groupBy.GetResult);

            public IObservable<T> Visit<A, B>(Product<T, A, B> product)
            {
                var a = Adapter.Get(product.InputA);
                var b = Adapter.Get(product.InputB);
                return a.SelectMany(ai => b.SelectMany(bi => product.Func(ai, bi)));
            }

            private HotObservableAdapter Adapter { get; }
        }

        private ObservableMap Map { get; }
            = new ObservableMap();
    }
}
