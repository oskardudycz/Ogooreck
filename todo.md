# Todo: Ogooreck 1.0 Emmett-shaped API

Plan: `plan.md` (r13). Spec: `spec.md` (r16).

## Steps

- [x] 1. Replace the 0.x API with the in-process ApiSpecification<TProgram>: requests and response expectations (1.1-1.2, 7.1, 8.1-8.4, 9.1, 10.1, 14.1-14.2, 23.1-23.13, 24.1-24.2, 25.1-25.3, 34.1-34.2, 35.1-35.2)
- [x] 2. Messaging in the Wolverine sample: recording, catch-up, Action and Publish (sections 2, 3: 2.1-2.3, 3.1, 11.1-11.3, 12.1-12.2, 15.1-15.5, 16.1, 16.3-16.4, 17.1-17.3, 21.1, 22.1-22.4)
- [x] 3. In-process traces and metrics, then Eventually (sections 4, 5: 6.1-6.5, 13.1-13.6, 26.1-26.6, 27.1-27.5, 28.1-28.7)
- [x] 4. Kafka, Marten and causation (runs in parallel with step 5) (sections 6, 7, 8: 18.1-18.4, 19.1-19.2, 20.1-20.5, 32.1)
- [x] 5. (parallel with step 4) Out-of-process apps: non-generic ApiSpecification, OtlpReceiver and Aspire (section 10: 4.1-4.6, 5.1, 29.1-29.5)
- [ ] 6. (6c waits for step 8) Enforce documented public API and 100% coverage, then F#, other test frameworks, README and migration guide (step 3x + sections 11, 12, 13: qa 55, 30.1, 31.1, 33.1)
- [x] 7. Plugs: one `Use(plug)` call per tool, with packages for Kafka, RabbitMQ, Marten and Wolverine (spec r14, criteria 36-41)
- [x] 8. Variants of the test setup on a shared app: `With` and `WithNewApp` (spec r15, criteria 2.4-2.6, qa 96)
