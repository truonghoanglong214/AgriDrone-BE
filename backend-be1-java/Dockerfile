FROM eclipse-temurin:21-jdk-alpine AS build
WORKDIR /workspace
COPY .mvn .mvn
COPY mvnw pom.xml ./
RUN sed -i 's/\r$//' mvnw \
    && chmod +x mvnw \
    && ./mvnw -B -ntp dependency:go-offline
COPY src src
RUN ./mvnw -B -ntp clean package -DskipTests

FROM eclipse-temurin:21-jre-alpine AS runtime
RUN addgroup -S agridrone && adduser -S -G agridrone agridrone
WORKDIR /app
COPY --from=build /workspace/target/agridrone-be1-*.jar app.jar
USER agridrone
EXPOSE 8080
ENTRYPOINT ["java", "-Duser.timezone=UTC", "-XX:MaxRAMPercentage=75.0", "-jar", "/app/app.jar"]
