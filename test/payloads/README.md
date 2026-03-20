```bash
cat ./test/payloads/machine-location-event.json | dcsp sb send --topic sb-loadtest-listen --event-type MachineLocationEvent --repeat-times 100

cat ./test/payloads/user-updated-event.json | dcsp sb send --topic sb-loadtest-listen --event-type UserUpdatedEvent --repeat-times 100
```