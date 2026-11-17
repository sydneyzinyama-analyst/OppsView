# OppsView notifier container (Java 25 + Spring Boot).

FROM eclipse-temurin:25-jdk AS build
WORKDIR /src
# The Maven wrapper and pom first, so dependencies are cached as a layer.
COPY mvnw pom.xml ./
COPY .mvn/ .mvn/
RUN ./mvnw --batch-mode dependency:go-offline
COPY src/ src/
RUN ./mvnw --batch-mode -DskipTests package && cp target/notifier-*.jar /app.jar

FROM eclipse-temurin:25-jre AS runtime
RUN useradd --create-home --uid 10001 notifier
USER notifier
COPY --from=build /app.jar /app/notifier.jar
# Same small-footprint settings as local runs: 256 MB heap, simple GC.
ENTRYPOINT ["java", "-Xmx256m", "-XX:+UseSerialGC", "-XX:TieredStopAtLevel=1", "-jar", "/app/notifier.jar"]
