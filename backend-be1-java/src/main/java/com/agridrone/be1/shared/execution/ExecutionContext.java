package com.agridrone.be1.shared.execution;

import java.util.Optional;

public final class ExecutionContext {
    private static final ThreadLocal<ExecutionContextSnapshot> CURRENT = new ThreadLocal<>();

    private ExecutionContext() {
    }

    public static Optional<ExecutionContextSnapshot> current() {
        return Optional.ofNullable(CURRENT.get());
    }

    public static ExecutionContextSnapshot require() {
        return current().orElseThrow(() -> new IllegalStateException("No execution context is active"));
    }

    public static Scope begin(ExecutionContextSnapshot snapshot) {
        if (CURRENT.get() != null) {
            throw new IllegalStateException("Nested execution contexts are not allowed");
        }
        CURRENT.set(snapshot);
        return () -> CURRENT.remove();
    }

    @FunctionalInterface
    public interface Scope extends AutoCloseable {
        @Override
        void close();
    }
}
