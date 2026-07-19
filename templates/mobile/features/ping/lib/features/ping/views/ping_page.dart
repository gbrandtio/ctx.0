import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../ping_cubit.dart';

/// A minimal screen that exercises the secure ping round trip end to end.
class PingPage extends StatefulWidget {
  const PingPage({super.key});

  @override
  State<PingPage> createState() => _PingPageState();
}

class _PingPageState extends State<PingPage> {
  final TextEditingController _controller = TextEditingController(text: 'marco');

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Secure ping')),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            TextField(
              controller: _controller,
              decoration: const InputDecoration(labelText: 'Message'),
            ),
            const SizedBox(height: 16),
            BlocBuilder<PingCubit, PingState>(
              builder: (context, state) {
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    FilledButton(
                      onPressed: state.status == PingStatus.sending
                          ? null
                          : () => context.read<PingCubit>().send(_controller.text),
                      child: Text(state.status == PingStatus.sending ? 'Sending…' : 'Send secure ping'),
                    ),
                    const SizedBox(height: 16),
                    if (state.status == PingStatus.success) Text('Echo: ${state.echo}'),
                    if (state.status == PingStatus.failure)
                      Text('Error: ${state.error}', style: const TextStyle(color: Colors.red)),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}
