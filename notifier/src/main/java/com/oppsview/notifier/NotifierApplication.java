package com.oppsview.notifier;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;
import org.springframework.scheduling.annotation.EnableScheduling;

/**
 * The OppsView notifier: a background worker (no web server) that listens
 * to application events from the .NET API on RabbitMQ, plans follow-up
 * reminders, and announces them on RabbitMQ when they're due.
 */
@SpringBootApplication
@EnableScheduling
@ConfigurationPropertiesScan
public class NotifierApplication {

    public static void main(String[] args) {
        SpringApplication.run(NotifierApplication.class, args);
    }
}
