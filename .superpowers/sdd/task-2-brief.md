### Task 2: ScriptableObject event channels

**Files:**
- Create: `Assets/Game/Scripts/Events/GameEventChannel.cs`
- Create: `Assets/Game/Scripts/Events/IntEventChannel.cs`
- Create: `Assets/Game/Scripts/Events/OrderEventChannel.cs`
- Create: `Assets/Game/Scripts/Events/OrderEventPayload.cs`
- Test: `Assets/Game/Tests/Events/GameEventChannelTests.cs`

**Interfaces:**
- Produces: `GameEventChannel.Raise()`, `GameEventChannel.Register(Action)`, `IntEventChannel.Raise(int)`, `OrderEventChannel.Raise(OrderEventPayload)`.

- [ ] **Step 1: Write failing test**

Create `Assets/Game/Tests/Events/GameEventChannelTests.cs`:

```csharp
using NUnit.Framework;
using TrafficSim.Events;

namespace TrafficSim.Tests.Events
{
    public class GameEventChannelTests
    {
        [Test]
        public void Raise_InvokesRegisteredListener()
        {
            var channel = UnityEngine.ScriptableObject.CreateInstance<GameEventChannel>();
            var count = 0;
            channel.Register(() => count++);
            channel.Raise();
            Assert.AreEqual(1, count);
        }
    }
}
```

- [ ] **Step 2: Run test — expect FAIL**

Unity: Window → General → Test Runner → Edit Mode → run `Raise_InvokesRegisteredListener` → FAIL (type not found).

- [ ] **Step 3: Implement event channels**

Create `Assets/Game/Scripts/Events/GameEventChannel.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficSim.Events
{
    [CreateAssetMenu(menuName = "TrafficSim/Events/Game Event")]
    public class GameEventChannel : ScriptableObject
    {
        readonly List<Action> _listeners = new();

        public void Register(Action listener) => _listeners.Add(listener);
        public void Unregister(Action listener) => _listeners.Remove(listener);

        public void Raise()
        {
            for (var i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i]?.Invoke();
        }
    }
}
```

Create `Assets/Game/Scripts/Events/IntEventChannel.cs` (same pattern with `Action<int>`).

Create `Assets/Game/Scripts/Events/OrderEventPayload.cs`:

```csharp
using TrafficSim.Core;

namespace TrafficSim.Events
{
    public readonly struct OrderEventPayload
    {
        public readonly int OrderId;
        public readonly ServiceModule Module;
        public OrderEventPayload(int orderId, ServiceModule module)
        {
            OrderId = orderId;
            Module = module;
        }
    }
}
```

Create `Assets/Game/Scripts/Events/OrderEventChannel.cs` with `Action<OrderEventPayload>`.

- [ ] **Step 4: Run test — expect PASS**

- [ ] **Step 5: Commit**

```bash
git add Assets/Game/Scripts/Events Assets/Game/Tests/Events
git commit -m "feat: add ScriptableObject event channels"
```
