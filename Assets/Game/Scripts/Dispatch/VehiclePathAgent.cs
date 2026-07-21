using System;
using System.Collections.Generic;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Dispatch
{
    public sealed class VehiclePathAgent
    {
        RoadGraph _graph;
        IReadOnlyList<int> _nodeIds;
        int _targetNodeIndex;
        float _speed;
        Action _onArrived;
        bool _active;

        public Vector3 Position { get; private set; }
        public bool IsActive => _active;

        public void SetPath(IReadOnlyList<int> nodeIds, RoadGraph graph)
        {
            _nodeIds = nodeIds;
            _graph = graph;
            _targetNodeIndex = 0;
            _speed = 0f;
            _onArrived = null;
            _active = false;

            if (_nodeIds == null || _nodeIds.Count == 0 || _graph == null)
                return;

            Position = _graph.GetNodePosition(_nodeIds[0]);
            _targetNodeIndex = _nodeIds.Count > 1 ? 1 : 0;
            _active = true;
        }

        public void Configure(float speed, Action onArrived)
        {
            _speed = Mathf.Max(0f, speed);
            _onArrived = onArrived;

            if (_active && _nodeIds != null && _nodeIds.Count == 1)
                Complete();
        }

        public bool Tick(float deltaTime)
        {
            if (!_active || _nodeIds == null || _graph == null || deltaTime <= 0f)
                return !_active;

            if (_nodeIds.Count == 1)
            {
                Complete();
                return true;
            }

            var distanceRemaining = _speed * deltaTime;

            while (distanceRemaining > 0f && _active)
            {
                var target = _graph.GetNodePosition(_nodeIds[_targetNodeIndex]);
                var segmentLength = Vector3.Distance(Position, target);

                if (segmentLength <= 0.0001f)
                {
                    Position = target;
                    if (_targetNodeIndex >= _nodeIds.Count - 1)
                    {
                        Complete();
                        return true;
                    }

                    _targetNodeIndex++;
                    continue;
                }

                if (distanceRemaining >= segmentLength)
                {
                    Position = target;
                    distanceRemaining -= segmentLength;

                    if (_targetNodeIndex >= _nodeIds.Count - 1)
                    {
                        Complete();
                        return true;
                    }

                    _targetNodeIndex++;
                    continue;
                }

                Position = Vector3.MoveTowards(Position, target, distanceRemaining);
                distanceRemaining = 0f;
            }

            return false;
        }

        void Complete()
        {
            _active = false;
            _onArrived?.Invoke();
        }
    }
}
