﻿using NLog.Fluent;
using PKHeX.Core;
using System;
using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public class TradeQueueManager<T> where T : PKM, new()
    {
        private readonly PokeTradeHub<T> Hub;

        private readonly PokeTradeQueue<T> Trade = new PokeTradeQueue<T>(PokeTradeType.Specific);
        private readonly PokeTradeQueue<T> Seed = new PokeTradeQueue<T>(PokeTradeType.Seed);
        private readonly PokeTradeQueue<T> Clone = new PokeTradeQueue<T>(PokeTradeType.Clone);
        private readonly PokeTradeQueue<T> FixOT = new PokeTradeQueue<T>(PokeTradeType.FixOT);
        private readonly PokeTradeQueue<T> PowerUp = new PokeTradeQueue<T>(PokeTradeType.PowerUp);
        private readonly PokeTradeQueue<T> EggRoll = new PokeTradeQueue<T>(PokeTradeType.EggRoll);
        private readonly PokeTradeQueue<T> Dump = new PokeTradeQueue<T>(PokeTradeType.Dump);
        private readonly PokeTradeQueue<T> LanTrade = new PokeTradeQueue<T>(PokeTradeType.LanTrade);
        private readonly PokeTradeQueue<T> LanRoll = new PokeTradeQueue<T>(PokeTradeType.LanRoll);
        public readonly TradeQueueInfo<T> Info;
        public readonly PokeTradeQueue<T>[] AllQueues;
        public readonly PokeTradeQueue<T>[] LanQueues;

        public TradeQueueManager(PokeTradeHub<T> hub)
        {
            Hub = hub;
            Info = new TradeQueueInfo<T>(hub);
            AllQueues = new[] { Seed, Dump, Clone, FixOT, PowerUp, EggRoll, LanTrade, LanRoll, Trade };
            LanQueues = new[] { LanTrade, LanRoll };

            foreach (var q in AllQueues)
                q.Queue.Settings = hub.Config.Favoritism;

            foreach (var q in LanQueues)
                q.Queue.Settings = hub.Config.Favoritism;
        }

        public PokeTradeQueue<T> GetQueue(PokeRoutineType type)
        {
            return type switch
            {
                PokeRoutineType.SeedCheck => Seed,
                PokeRoutineType.Clone => Clone,
                PokeRoutineType.FixOT => FixOT,
                PokeRoutineType.PowerUp => PowerUp,
                PokeRoutineType.EggRoll => EggRoll,
                PokeRoutineType.Dump => Dump,
                PokeRoutineType.LanTrade => LanTrade,
                PokeRoutineType.LanRoll => LanRoll,
                _ => Trade,
            };
        }

        public void ClearAll()
        {
            foreach (var q in AllQueues)
                q.Clear();
        }

        public bool TryDequeueLedy(out PokeTradeDetail<T> detail)
        {
            detail = default!;
            var cfg = Hub.Config.Distribution;
            if (!cfg.DistributeWhileIdle)
                return false;

            if (Hub.Ledy.Pool.Count == 0)
                return false;

            var random = Hub.Ledy.Pool.GetRandomPoke();
            var code = cfg.RandomCode ? Hub.Config.Trade.GetRandomTradeCode() : cfg.TradeCode;
            var trainer = new PokeTradeTrainerInfo("Random Distribution");
            detail = new PokeTradeDetail<T>(random, trainer, PokeTradeHub<T>.LogNotifier, PokeTradeType.Random, code, detail.DiscordUserId, false);
            return true;
        }

        public bool TryDequeue(PokeRoutineType type, out PokeTradeDetail<T> detail, out uint priority)
        {
            if (type == PokeRoutineType.FlexTrade)
                return GetFlexDequeue(out detail, out priority);

            var cfg = Hub.Config.Queues;
            if (type == PokeRoutineType.LanTrade)
                return GetLanDequeue(cfg, out detail, out priority);

            return TryDequeueInternal(type, out detail, out priority);
        }

        private bool TryDequeueInternal(PokeRoutineType type, out PokeTradeDetail<T> detail, out uint priority)
        {
            var queue = GetQueue(type);
            return queue.TryDequeue(out detail, out priority);
        }

        private bool GetFlexDequeue(out PokeTradeDetail<T> detail, out uint priority)
        {
            var cfg = Hub.Config.Queues;
            if (cfg.FlexMode == FlexYieldMode.LessCheatyFirst)
                return GetFlexDequeueOld(out detail, out priority);
            return GetFlexDequeueWeighted(cfg, out detail, out priority);
        }

        private bool GetFlexDequeueWeighted(QueueSettings cfg, out PokeTradeDetail<T> detail, out uint priority)
        {
            PokeTradeQueue<T>? preferredQueue = null;
            long bestWeight = 0; // prefer higher weights
            uint bestPriority = uint.MaxValue; // prefer smaller
            foreach (var q in AllQueues)
            {
                var peek = q.TryPeek(out detail, out priority);
                if (!peek)
                    continue;

                // priority queue is a min-queue, so prefer smaller priorities
                if (priority > bestPriority)
                    continue;

                var count = q.Count;
                var time = detail.Time;
                var weight = cfg.GetWeight(count, time, q.Type);

                if (priority >= bestPriority && weight <= bestWeight)
                    continue; // not good enough to be preferred over the other.

                // this queue has the most preferable priority/weight so far!
                bestWeight = weight;
                bestPriority = priority;
                preferredQueue = q;
            }

            if (preferredQueue == null)
            {
                detail = default!;
                priority = default;
                return false;
            }

            return preferredQueue.TryDequeue(out detail, out priority);
        }

        private bool GetFlexDequeueOld(out PokeTradeDetail<T> detail, out uint priority)
        {
            if (TryDequeueInternal(PokeRoutineType.SeedCheck, out detail, out priority))
                return true;
            if (TryDequeueInternal(PokeRoutineType.Clone, out detail, out priority))
                return true;
            if (TryDequeueInternal(PokeRoutineType.FixOT, out detail, out priority))
                return true;
            if (TryDequeueInternal(PokeRoutineType.PowerUp, out detail, out priority))
                return true;
            if (TryDequeueInternal(PokeRoutineType.EggRoll, out detail, out priority))
                return true;
            if (TryDequeueInternal(PokeRoutineType.Dump, out detail, out priority))
                return true;
            if (TryDequeueInternal(PokeRoutineType.LinkTrade, out detail, out priority))
                return true;
            return false;
        }

        private bool GetLanDequeue(QueueSettings cfg, out PokeTradeDetail<T> detail, out uint priority)
        {
            PokeTradeQueue<T>? preferredQueue = null;
            long bestWeight = 0; // prefer higher weights
            uint bestPriority = uint.MaxValue; // prefer smaller
            foreach (var q in LanQueues)
            {
                var peek = q.TryPeek(out detail, out priority);
                if (!peek)
                    continue;

                // priority queue is a min-queue, so prefer smaller priorities
                if (priority > bestPriority)
                    continue;

                var count = q.Count;
                var time = detail.Time;
                var weight = cfg.GetWeight(count, time, q.Type);

                if (priority >= bestPriority && weight <= bestWeight)
                    continue; // not good enough to be preferred over the other.

                // this queue has the most preferable priority/weight so far!
                bestWeight = weight;
                bestPriority = priority;
                preferredQueue = q;
            }

            if (preferredQueue == null)
            {
                detail = default!;
                priority = default;
                return false;
            }

            return preferredQueue.TryDequeue(out detail, out priority);
        }

        public void Enqueue(PokeRoutineType type, PokeTradeDetail<T> detail, uint priority)
        {
            var queue = GetQueue(type);
            queue.Enqueue(detail, priority);
        }

        // hook in here if you want to forward the message elsewhere???
        public readonly List<Action<PokeTradeBot, PokeTradeDetail<T>>> Forwarders = new List<Action<PokeTradeBot, PokeTradeDetail<T>>>();

        public void StartTrade(PokeTradeBot b, PokeTradeDetail<T> detail)
        {
            foreach (var f in Forwarders)
                f.Invoke(b, detail);
        }
    }
}
